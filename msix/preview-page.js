const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');

const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'rintas-preview-'));
const file = path.join(dataDir, 'Contoh dokumen Rintas.txt');
fs.writeFileSync(file, 'Contoh unduhan Rintas');
fs.writeFileSync(path.join(dataDir, 'config.json'), JSON.stringify({ publicBaseUrl: '', defaultExpireMinutes: 60 }));
const child = spawn(process.execPath, [path.resolve(__dirname, '..', 'share.js')], {
  env: {
    ...process.env,
    RINTAS_DATA_DIR: dataDir,
    RINTAS_TEST_PUBLIC_PORT: '18765',
    RINTAS_TEST_CONTROL_PORT: '18766',
    RINTAS_TEST_BASE_URL: 'http://127.0.0.1:18765',
  },
  stdio: 'ignore',
  windowsHide: true,
});

function share(token) {
  return new Promise((resolve, reject) => {
    const req = http.request({ hostname: '127.0.0.1', port: 18766, path: '/share', method: 'POST', headers: { 'X-Rintas-Control': token } }, (res) => {
      let body = '';
      res.on('data', (chunk) => { body += chunk; });
      res.on('end', () => res.statusCode === 200 ? resolve(body) : reject(new Error(body)));
    });
    req.on('error', reject);
    req.end(file);
  });
}

async function main() {
  for (let i = 0; i < 100; i++) {
    try {
      const token = fs.readFileSync(path.join(dataDir, 'control.token'), 'utf8').trim();
      const url = await share(token);
      console.log(url);
      return;
    } catch { await new Promise((resolve) => setTimeout(resolve, 100)); }
  }
  throw new Error('Server pratinjau tidak siap.');
}

function stop() {
  child.kill();
  fs.rmSync(dataDir, { recursive: true, force: true });
  process.exit();
}
process.on('SIGINT', stop);
process.on('SIGTERM', stop);
main().then(() => process.stdin.resume()).catch((error) => { console.error(error); stop(); });
