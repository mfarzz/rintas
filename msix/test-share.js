const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const net = require('node:net');
const http = require('node:http');
const { spawn } = require('node:child_process');

const root = path.resolve(__dirname, '..');
const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'rintas-test-'));

async function freePort() {
  const server = net.createServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const port = server.address().port;
  await new Promise((resolve) => server.close(resolve));
  return port;
}

function request(port, method, route, token, body = '') {
  return new Promise((resolve, reject) => {
    const headers = token ? { 'X-Rintas-Control': token } : {};
    const req = http.request({ hostname: '127.0.0.1', port, method, path: route, headers }, (res) => {
      const chunks = [];
      res.on('data', (chunk) => chunks.push(chunk));
      res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(chunks).toString('utf8') }));
    });
    req.on('error', reject);
    req.end(body);
  });
}

async function main() {
  const publicPort = await freePort();
  const controlPort = await freePort();
  const base = `http://127.0.0.1:${publicPort}`;
  const file = path.join(dataDir, 'contoh & catatan.txt');
  fs.writeFileSync(file, 'isi contoh');
  fs.writeFileSync(path.join(dataDir, 'config.json'), JSON.stringify({ publicBaseUrl: '', defaultExpireMinutes: 1 }));
  const child = spawn(process.execPath, [path.join(root, 'share.js')], {
    env: { ...process.env, RINTAS_DATA_DIR: dataDir, RINTAS_TEST_PUBLIC_PORT: String(publicPort), RINTAS_TEST_CONTROL_PORT: String(controlPort), RINTAS_TEST_BASE_URL: base },
    windowsHide: true,
    stdio: 'ignore',
  });
  try {
    let token;
    let status;
    for (let i = 0; i < 100; i++) {
      try {
        token = fs.readFileSync(path.join(dataDir, 'control.token'), 'utf8').trim();
        status = await request(controlPort, 'GET', '/status', token);
        if (status.status === 200) break;
      } catch { /* server is starting */ }
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    assert.equal(status?.status, 200, 'control server did not start');
    assert.equal(JSON.parse(status.body).defaultExpireMinutes, 1);
    const unknown = await request(publicPort, 'GET', '/not-a-share');
    assert.equal(unknown.status, 404);
    assert.match(unknown.headers['content-type'], /^text\/html/);
    assert.match(unknown.body, /Tautan tidak ditemukan/);
    assert.match(unknown.body, /<picture><source srcset="data:image\/png;base64,/);
    const share = await request(controlPort, 'POST', '/share', token, file);
    assert.equal(share.status, 200);
    assert.match(share.body, new RegExp(`^${base.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/[A-Za-z0-9_-]{16}$`));
    const route = new URL(share.body).pathname;
    const listed = await request(controlPort, 'GET', '/shares', token);
    assert.equal(listed.status, 200);
    assert.equal(JSON.parse(listed.body).length, 1);
    assert.equal(JSON.parse(listed.body)[0].url, share.body);
    assert.equal(JSON.parse(listed.body)[0].name, 'contoh & catatan.txt');
    const page = await request(publicPort, 'GET', route);
    assert.equal(page.status, 200);
    assert.match(page.body, /contoh &amp; catatan\.txt/);
    assert.match(page.body, /<picture><source srcset="data:image\/png;base64,[^"]+" media="\(prefers-color-scheme: dark\)"><img class="mark" src="data:image\/png;base64,[^"]+" alt=""><\/picture>/);
    assert.match(page.body, /@media\(prefers-color-scheme:dark\)/);
    const marks = page.body.match(/<picture><source srcset="data:image\/png;base64,([^"]+)"[^>]*><img class="mark" src="data:image\/png;base64,([^"]+)"/);
    assert.deepEqual(Buffer.from(marks[1], 'base64'), fs.readFileSync(path.join(root, 'assets', 'rintas-mark.png')));
    assert.deepEqual(Buffer.from(marks[2], 'base64'), fs.readFileSync(path.join(root, 'assets', 'rintas-mark-1.png')));
    assert.match(page.body, /border-radius:0;box-shadow:none/);
    assert.equal((await request(publicPort, 'GET', `${route}/download`)).body, 'isi contoh');
    const second = await request(controlPort, 'POST', '/share', token, file);
    assert.equal(second.status, 200);
    const secondKey = new URL(second.body).pathname.slice(1);
    assert.equal((await request(controlPort, 'POST', '/revoke', token, secondKey)).status, 200);
    assert.equal((await request(publicPort, 'GET', new URL(second.body).pathname)).status, 404);
    assert.equal(JSON.parse((await request(controlPort, 'GET', '/shares', token)).body).length, 1);
    const inactive = await request(publicPort, 'GET', '/ABCDEFGHIJKLMNOP');
    assert.equal(inactive.status, 404);
    assert.match(inactive.body, /Tautan tidak aktif/);
    fs.unlinkSync(file);
    const unavailable = await request(publicPort, 'GET', route);
    assert.equal(unavailable.status, 404);
    assert.match(unavailable.body, /File tidak tersedia/);
    fs.writeFileSync(file, 'isi contoh');
    await new Promise((resolve) => setTimeout(resolve, 61000));
    const expired = await request(publicPort, 'GET', route);
    assert.ok([404, 410].includes(expired.status));
    assert.match(expired.body, /Tautan tidak aktif|Masa berlaku tautan habis/);
    assert.match(expired.headers['content-type'], /^text\/html/);
    assert.equal((await request(publicPort, 'GET', `${route}/download`)).status, 404);
    assert.equal(JSON.parse((await request(controlPort, 'GET', '/status', token)).body).count, 0);
    console.log('Tautan pendek, unduhan, dan kedaluwarsa: lulus');
  } finally {
    child.kill();
    fs.rmSync(dataDir, { recursive: true, force: true });
  }
}

main().catch((error) => { console.error(error); process.exitCode = 1; });
