const http = require('node:http');
const https = require('node:https');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const dns = require('node:dns');
const { spawn } = require('node:child_process');

const host = '127.0.0.1';
const publicPort = Number(process.env.RINTAS_TEST_PUBLIC_PORT || 8765);
const controlPort = Number(process.env.RINTAS_TEST_CONTROL_PORT || 8766);
const dataDir = process.env.RINTAS_DATA_DIR || path.join(process.env.LOCALAPPDATA || __dirname, 'FileShareTunnel');
const tokenPath = path.join(dataDir, 'control.token');
const logPath = path.join(dataDir, 'rintas.log');
const dataConfig = path.join(dataDir, 'config.json');
const configPath = fs.existsSync(dataConfig) ? dataConfig : path.join(__dirname, 'config.json');
const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
function markDataUri(name, sourceName) {
  const packaged = path.join(__dirname, 'Assets', name);
  const source = path.join(__dirname, 'assets', sourceName);
  return `data:image/png;base64,${fs.readFileSync(fs.existsSync(packaged) ? packaged : source).toString('base64')}`;
}
const darkMark = markDataUri('BrandMarkDark.png', 'rintas-mark.png');
const lightMark = markDataUri('BrandMarkLight.png', 'rintas-mark-1.png');
const configuredBase = (process.env.PUBLIC_BASE_URL || config.publicBaseUrl || '').replace(/\/$/, '');
const defaultExpireMinutes = config.defaultExpireMinutes === undefined ? 1440 : config.defaultExpireMinutes;
const testBase = process.env.RINTAS_TEST_BASE_URL || '';
const customDomain = Boolean(configuredBase && !testBase);
const healthPath = `/__rintas_health/${crypto.randomBytes(18).toString('base64url')}`;
const healthMarker = crypto.randomBytes(24).toString('hex');
if (configuredBase && !/^https:\/\/[^/]+$/.test(configuredBase)) throw new Error('publicBaseUrl harus berupa domain HTTPS.');
if (!Number.isInteger(defaultExpireMinutes) || defaultExpireMinutes < 1 || defaultExpireMinutes > 10080) throw new Error('defaultExpireMinutes harus 1 sampai 10080 menit.');
fs.mkdirSync(dataDir, { recursive: true });
let controlToken;
try {
  controlToken = fs.readFileSync(tokenPath, 'utf8').trim();
  if (!/^[a-f0-9]{64}$/.test(controlToken)) throw new Error('Token kontrol tidak valid.');
} catch (error) {
  if (error.code !== 'ENOENT') throw error;
  controlToken = crypto.randomBytes(32).toString('hex');
  fs.writeFileSync(tokenPath, controlToken, { flag: 'wx', mode: 0o600 });
}

function log(message) {
  fs.appendFileSync(logPath, `${new Date().toISOString()} ${message}\n`);
}

let baseUrl = testBase || configuredBase || '';
let domainReady = !customDomain;
let domainError = customDomain ? 'Domain sedang diperiksa. Pastikan Cloudflare Tunnel mengarah ke http://127.0.0.1:8765.' : '';
let domainCheckRunning = false;
let tunnel;
let tunnelError = '';
let stopping = false;
const shares = new Map();
const publicResolver = new dns.Resolver();
publicResolver.setServers(['1.1.1.1', '8.8.8.8']);

function lookupDomain(hostname, options, callback) {
  dns.lookup(hostname, options, (error, address, family) => {
    if (!error) return callback(null, address, family);
    if (error.code !== 'ENOTFOUND' && error.code !== 'EAI_AGAIN') return callback(error);
    publicResolver.resolve4(hostname, (fallbackError, addresses) => {
      if (fallbackError || !addresses || addresses.length === 0) return callback(error);
      if (options && options.all) return callback(null, addresses.map((item) => ({ address: item, family: 4 })));
      callback(null, addresses[0], 4);
    });
  });
}

function publicReady() {
  return Boolean(baseUrl) && domainReady;
}

function checkCustomDomain() {
  if (!customDomain || domainCheckRunning || stopping) return;
  domainCheckRunning = true;
  const request = https.get(`${configuredBase}${healthPath}`, { timeout: 8000, lookup: lookupDomain, headers: { 'Cache-Control': 'no-cache' } }, (response) => {
    let body = '';
    response.setEncoding('utf8');
    response.on('data', (chunk) => {
      body += chunk;
      if (body.length > 128) request.destroy(new Error('Respons domain tidak sesuai.'));
    });
    response.on('end', () => {
      domainCheckRunning = false;
      domainReady = response.statusCode === 200 && body === healthMarker;
      domainError = domainReady ? '' : 'Domain belum mengarah ke Rintas. Atur Cloudflare Named Tunnel dan rute hostname ke http://127.0.0.1:8765.';
    });
  });
  request.on('timeout', () => request.destroy(new Error('Waktu pemeriksaan domain habis.')));
  request.on('error', (error) => {
    domainCheckRunning = false;
    domainReady = false;
    domainError = `Domain belum terhubung ke Rintas: ${error.message}`;
  });
}

function removeExpired() {
  const now = Date.now();
  for (const [key, share] of shares) {
    if (now >= share.expiresAt) shares.delete(key);
  }
}

setInterval(removeExpired, 60000).unref();

function equalToken(a, b) {
  const first = Buffer.from(a || '');
  const second = Buffer.from(b || '');
  return first.length === second.length && crypto.timingSafeEqual(first, second);
}

function sendText(response, status, text) {
  response.writeHead(status, { 'Content-Type': 'text/plain; charset=utf-8', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' });
  response.end(text);
}

function sendUnavailable(request, response, status, reason) {
  const messages = {
    missing: ['TAUTAN TIDAK DITEMUKAN', 'Tautan tidak ditemukan', 'Periksa alamat tautan atau minta tautan baru kepada pengirim.'],
    inactive: ['TAUTAN TIDAK AKTIF', 'Tautan tidak aktif', 'Pengirim mungkin telah mencabut tautan atau menghentikan Rintas. Minta tautan baru kepada pengirim.'],
    expired: ['TAUTAN KEDALUWARSA', 'Masa berlaku tautan habis', 'Minta pengirim membuat tautan baru untuk file ini.'],
    file: ['FILE TIDAK TERSEDIA', 'File tidak tersedia', 'File asal mungkin dipindahkan atau dihapus. Hubungi pengirim untuk meminta tautan baru.'],
  };
  const [eyebrow, title, detail] = messages[reason];
  const html = `<!doctype html>
<html lang="id">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="referrer" content="no-referrer">
  <meta name="theme-color" content="#F4F4F6" media="(prefers-color-scheme: light)">
  <meta name="theme-color" content="#14141E" media="(prefers-color-scheme: dark)">
  <link rel="icon" href="${lightMark}" media="(prefers-color-scheme: light)">
  <link rel="icon" href="${darkMark}" media="(prefers-color-scheme: dark)">
  <title>${title} — Rintas</title>
  <style>
    :root{color-scheme:light dark;--ink:#14141E;--paper:#F4F4F6;--surface:#fff;--muted:#595D69;--line:#C8CBD2;--teal:#4FD1C5}
    *{box-sizing:border-box;border-radius:0;box-shadow:none}
    body{margin:0;min-height:100vh;background:var(--paper);color:var(--ink);font:16px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif;display:grid;place-items:center;padding:24px}
    .shell{width:min(100%,560px);border:1px solid var(--ink);background:var(--surface)}
    header{display:flex;align-items:center;gap:12px;padding:18px 24px;border-bottom:1px solid var(--line)}
    .mark{display:block;width:44px;height:44px;image-rendering:pixelated}
    .brand{font-size:18px;font-weight:800;letter-spacing:.12em}
    main{padding:32px 24px 40px}
    .eyebrow{margin:0 0 12px;font-size:12px;font-weight:800;letter-spacing:.14em}
    h1{margin:0;font-size:clamp(24px,5vw,32px);line-height:1.2}
    .detail{margin:20px 0 0;padding:18px 0 0;border-top:1px solid var(--line);color:var(--muted)}
    .indicator{display:inline-block;width:12px;height:12px;margin-right:10px;background:var(--teal)}
    footer{border-top:1px solid var(--line);padding:14px 24px;color:var(--muted);font-size:12px}
    @media(prefers-color-scheme:dark){:root{--ink:#fff;--paper:#14141E;--surface:#1D1D27;--muted:#B9BBC5;--line:#50515D}}
    @media(max-width:420px){main{padding:28px 20px 32px}header,footer{padding-left:20px;padding-right:20px}}
  </style>
</head>
<body>
  <div class="shell">
    <header><picture><source srcset="${darkMark}" media="(prefers-color-scheme: dark)"><img class="mark" src="${lightMark}" alt=""></picture><span class="brand">RINTAS</span></header>
    <main><p class="eyebrow"><span class="indicator" aria-hidden="true"></span>${eyebrow} · ${status}</p><h1>${title}</h1><p class="detail">${detail}</p></main>
    <footer>Dibagikan melalui Rintas</footer>
  </div>
</body>
</html>`;
  response.writeHead(status, { 'Content-Type': 'text/html; charset=utf-8', 'Content-Length': Buffer.byteLength(html), 'Cache-Control': 'private, no-store' });
  response.end(request.method === 'HEAD' ? undefined : html);
}

function formatSize(bytes) {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${bytes} B`;
}

function download(request, response, file) {
  let size;
  try { size = fs.statSync(file).size; }
  catch { return sendUnavailable(request, response, 404, 'file'); }
  let start = 0;
  let end = size - 1;
  let status = 200;
  const headers = {
    'Content-Type': 'application/octet-stream',
    'Content-Disposition': `attachment; filename="download"; filename*=UTF-8''${encodeURIComponent(path.basename(file))}`,
    'Accept-Ranges': 'bytes',
    'Cache-Control': 'private, no-store',
    'Referrer-Policy': 'no-referrer',
    'X-Content-Type-Options': 'nosniff',
  };
  if (request.headers.range) {
    const match = /^bytes=(\d*)-(\d*)$/.exec(request.headers.range);
    if (!match || (!match[1] && !match[2])) {
      response.writeHead(416, { ...headers, 'Content-Range': `bytes */${size}` });
      return response.end();
    }
    if (match[1]) {
      start = Number(match[1]);
      if (match[2]) end = Math.min(Number(match[2]), size - 1);
    } else start = Math.max(0, size - Number(match[2]));
    if (!Number.isSafeInteger(start) || !Number.isSafeInteger(end) || start >= size || start > end) {
      response.writeHead(416, { ...headers, 'Content-Range': `bytes */${size}` });
      return response.end();
    }
    status = 206;
    headers['Content-Range'] = `bytes ${start}-${end}/${size}`;
  }
  headers['Content-Length'] = end - start + 1;
  response.writeHead(status, headers);
  if (request.method === 'HEAD') return response.end();
  const stream = fs.createReadStream(file, { start, end });
  stream.on('error', () => response.destroy());
  stream.pipe(response);
}

const publicServer = http.createServer((request, response) => {
  response.setHeader('Referrer-Policy', 'no-referrer');
  response.setHeader('X-Content-Type-Options', 'nosniff');
  if (request.method !== 'GET' && request.method !== 'HEAD') return sendText(response, 405, 'Metode tidak didukung.');
  let pathname;
  try { pathname = new URL(request.url, `http://${host}:${publicPort}`).pathname; }
  catch { return sendText(response, 400, 'Alamat tidak valid.'); }
  if (pathname === healthPath) return sendText(response, 200, healthMarker);
  const match = /^\/([A-Za-z0-9_-]{16})(\/download)?$/.exec(pathname);
  if (!match) return sendUnavailable(request, response, 404, 'missing');
  const share = shares.get(match[1]);
  if (!share) return sendUnavailable(request, response, 404, 'inactive');
  if (Date.now() >= share.expiresAt) {
    shares.delete(match[1]);
    return sendUnavailable(request, response, 410, 'expired');
  }
  const file = share.file;
  if (match[2]) return download(request, response, file);
  let size;
  try { size = fs.statSync(file).size; }
  catch { return sendUnavailable(request, response, 404, 'file'); }
  const name = path.basename(file).replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
  const expires = new Date(share.expiresAt).toISOString();
  const html = `<!doctype html>
<html lang="id">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <meta name="referrer" content="no-referrer">
  <meta name="theme-color" content="#F4F4F6" media="(prefers-color-scheme: light)">
  <meta name="theme-color" content="#14141E" media="(prefers-color-scheme: dark)">
  <link rel="icon" href="${lightMark}" media="(prefers-color-scheme: light)">
  <link rel="icon" href="${darkMark}" media="(prefers-color-scheme: dark)">
  <title>${name} — Rintas</title>
  <style>
    :root{color-scheme:light dark;--ink:#14141E;--paper:#F4F4F6;--surface:#fff;--muted:#595D69;--line:#C8CBD2;--button:#14141E;--button-text:#fff;--hover:#292A35;--teal:#4FD1C5}
    *{box-sizing:border-box;border-radius:0;box-shadow:none}
    body{margin:0;min-height:100vh;background:var(--paper);color:var(--ink);font:16px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif;display:grid;place-items:center;padding:24px}
    .shell{width:min(100%,560px);border:1px solid var(--ink);background:var(--surface)}
    header{display:flex;align-items:center;gap:12px;padding:18px 24px;border-bottom:1px solid var(--line);background:var(--surface);color:var(--ink)}
    .mark{display:block;width:44px;height:44px;image-rendering:pixelated}
    .brand{font-size:18px;font-weight:800;letter-spacing:.12em}
    main{padding:32px 24px 36px}
    .eyebrow{margin:0 0 12px;font-size:12px;font-weight:800;letter-spacing:.14em}
    h1{margin:0;overflow-wrap:anywhere;font-size:clamp(24px,5vw,32px);line-height:1.2;font-weight:750}
    .meta{display:grid;grid-template-columns:1fr 1fr;gap:20px;margin:28px 0 30px;padding:18px 0;border-top:1px solid var(--line);border-bottom:1px solid var(--line)}
    .meta span{display:block;color:var(--muted);font-size:11px;font-weight:700;letter-spacing:.1em}
    .meta strong{display:block;margin-top:6px;font-size:15px;font-weight:650}
    .download{display:flex;align-items:center;justify-content:space-between;gap:16px;min-height:56px;padding:14px 18px;background:var(--button);color:var(--button-text);text-decoration:none;font-size:14px;font-weight:800;letter-spacing:.08em}
    .download:hover,.download:focus-visible{background:var(--hover);outline:2px solid var(--teal);outline-offset:2px}
    .pixel{display:inline-block;flex:none;width:12px;height:12px;margin-right:12px;background:var(--teal)}
    .action{display:flex;align-items:center}
    .arrow{font-size:20px;line-height:1}
    .note{margin:20px 0 0;color:var(--muted);font-size:13px}
    footer{border-top:1px solid var(--line);padding:14px 24px;color:var(--muted);font-size:12px}
    @media(prefers-color-scheme:dark){:root{--ink:#fff;--paper:#14141E;--surface:#1D1D27;--muted:#B9BBC5;--line:#50515D;--button:#fff;--button-text:#14141E;--hover:#DADBE0}}
    @media(max-width:420px){main{padding:28px 20px 32px}header,footer{padding-left:20px;padding-right:20px}.meta{grid-template-columns:1fr;gap:14px}}
  </style>
</head>
<body>
  <div class="shell">
    <header><picture><source srcset="${darkMark}" media="(prefers-color-scheme: dark)"><img class="mark" src="${lightMark}" alt=""></picture><span class="brand">RINTAS</span></header>
    <main>
      <p class="eyebrow">FILE SIAP DIUNDUH</p>
      <h1>${name}</h1>
      <div class="meta">
        <div><span>UKURAN FILE</span><strong>${formatSize(size)}</strong></div>
        <div><span>TAUTAN BERLAKU HINGGA</span><strong><time id="expiry" datetime="${expires}">${expires}</time></strong></div>
      </div>
      <a class="download" href="/${match[1]}/download"><span class="action"><span class="pixel" aria-hidden="true"></span>UNDUH FILE</span><span class="arrow" aria-hidden="true">↗</span></a>
      <p class="note">File dikirim dari perangkat pengirim. Unduhan tersedia selama Rintas berjalan dan tautan masih berlaku.</p>
    </main>
    <footer>Dibagikan melalui Rintas</footer>
  </div>
  <script>const t=document.getElementById('expiry');t.textContent=new Date(t.dateTime).toLocaleString('id-ID',{dateStyle:'medium',timeStyle:'short'});</script>
</body>
</html>`;
  response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8', 'Content-Length': Buffer.byteLength(html), 'Cache-Control': 'private, no-store' });
  response.end(request.method === 'HEAD' ? undefined : html);
});

const controlServer = http.createServer((request, response) => {
  if (!equalToken(request.headers['x-rintas-control'], controlToken)) return sendText(response, 403, 'Akses ditolak.');
  if (request.headers.origin) return sendText(response, 403, 'Akses browser ditolak.');
  if (request.method === 'GET' && request.url === '/status') {
    removeExpired();
    return sendText(response, 200, JSON.stringify({ ready: publicReady(), count: shares.size, baseUrl, error: domainError || tunnelError, defaultExpireMinutes }));
  }
  if (request.method === 'GET' && request.url === '/shares') {
    removeExpired();
    const active = Array.from(shares, ([key, share]) => ({
      key, name: path.basename(share.file), url: share.url,
      expiresAt: new Date(share.expiresAt).toISOString(), createdAt: new Date(share.createdAt).toISOString(),
    })).sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    return sendText(response, 200, JSON.stringify(active));
  }
  if (request.method !== 'POST') return sendText(response, 405, 'Metode tidak didukung.');
  if (request.url === '/clear') {
    const count = shares.size;
    shares.clear();
    return sendText(response, 200, String(count));
  }
  if (request.url === '/shutdown') {
    sendText(response, 200, 'Rintas berhenti.');
    setImmediate(stop);
    return;
  }
  if (request.url === '/revoke') {
    let key = '';
    request.setEncoding('utf8');
    request.on('data', (chunk) => {
      key += chunk;
      if (key.length > 128) request.destroy();
    });
    request.on('end', () => {
      if (!/^[A-Za-z0-9_-]{16}$/.test(key)) return sendText(response, 400, 'Kunci tautan tidak valid.');
      return shares.delete(key)
        ? sendText(response, 200, 'Tautan dicabut.')
        : sendText(response, 404, 'Tautan sudah tidak aktif.');
    });
    return;
  }
  if (request.url !== '/share') return sendText(response, 404, 'Perintah tidak ditemukan.');
  if (!publicReady()) return sendText(response, 503, domainError || tunnelError || 'Tunnel sedang tersambung.');
  let body = '';
  request.setEncoding('utf8');
  request.on('data', (chunk) => {
    body += chunk;
    if (body.length > 32768) request.destroy();
  });
  request.on('end', () => {
    let file;
    try {
      file = fs.realpathSync(body);
      if (!fs.statSync(file).isFile()) throw new Error('Pilihan bukan file.');
    } catch { return sendText(response, 400, 'File tidak dapat dibuka.'); }
    removeExpired();
    const key = crypto.randomBytes(12).toString('base64url');
    const url = `${baseUrl}/${key}`;
    shares.set(key, { file, url, createdAt: Date.now(), expiresAt: Date.now() + defaultExpireMinutes * 60000 });
    log(`Berbagi ${path.basename(file)}; sesi aktif ${shares.size}`);
    sendText(response, 200, url);
  });
});

function stop() {
  if (stopping) return;
  stopping = true;
  shares.clear();
  tunnel?.kill();
  publicServer.close();
  controlServer.close();
  publicServer.closeAllConnections();
  controlServer.closeAllConnections();
}
process.on('SIGINT', stop);
process.on('SIGTERM', stop);

publicServer.on('error', (error) => { log(`Server publik gagal: ${error.message}`); process.exit(1); });
controlServer.on('error', (error) => { log(`Server kontrol gagal: ${error.message}`); process.exit(1); });
publicServer.listen(publicPort, host, () => {
  controlServer.listen(controlPort, host, () => {
    log(`Rintas aktif pada ${host}:${publicPort}`);
    if (customDomain) {
      checkCustomDomain();
      setInterval(checkCustomDomain, 30000).unref();
    }
    if (baseUrl) return;
    const bundled = path.join(__dirname, 'cloudflared.exe');
    const cloudflared = fs.existsSync(bundled) ? bundled : 'cloudflared';
    tunnel = spawn(cloudflared, ['tunnel', '--url', `http://${host}:${publicPort}`], { windowsHide: true });
    let output = '';
    tunnel.on('error', (error) => { tunnelError = error.message; log(`Tunnel gagal: ${error.message}`); });
    tunnel.stderr?.on('data', (chunk) => {
      output = (output + chunk.toString()).slice(-4096);
      const match = output.match(/https:\/\/[a-z0-9-]+\.trycloudflare\.com/i);
      if (match && !baseUrl) {
        baseUrl = match[0];
        log(`Tunnel siap: ${baseUrl}`);
      }
    });
    tunnel.on('exit', (code) => {
      if (!stopping) {
        baseUrl = '';
        tunnelError = `Cloudflare Tunnel berhenti (${code}). Jalankan ulang Rintas.`;
        log(tunnelError);
      }
    });
  });
});
