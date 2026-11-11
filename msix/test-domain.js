const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const net = require('node:net');
const http = require('node:http');
const { spawn } = require('node:child_process');

async function freePort() {
  const server = net.createServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const port = server.address().port;
  await new Promise((resolve) => server.close(resolve));
  return port;
}

function request(port, method, route, token, body = '') {
  return new Promise((resolve, reject) => {
    const req = http.request({ hostname: '127.0.0.1', port, path: route, method, headers: { 'X-Rintas-Control': token } }, (res) => {
      let text = '';
      res.setEncoding('utf8');
      res.on('data', (chunk) => { text += chunk; });
      res.on('end', () => resolve({ status: res.statusCode, body: text }));
    });
    req.on('error', reject);
    req.end(body);
  });
}

async function main() {
  const dataDir = fs.mkdtempSync(path.join(os.tmpdir(), 'rintas-domain-test-'));
  const publicPort = await freePort();
  const controlPort = await freePort();
  const file = path.join(dataDir, 'example.txt');
  fs.writeFileSync(file, 'test');
  fs.writeFileSync(path.join(dataDir, 'config.json'), JSON.stringify({ publicBaseUrl: 'https://127.0.0.1:9', defaultExpireMinutes: 60 }));
  const child = spawn(process.execPath, [path.resolve(__dirname, '..', 'share.js')], {
    env: { ...process.env, RINTAS_DATA_DIR: dataDir, RINTAS_TEST_PUBLIC_PORT: String(publicPort), RINTAS_TEST_CONTROL_PORT: String(controlPort) },
    windowsHide: true,
    stdio: 'ignore',
  });
  try {
    let status;
    let token;
    for (let i = 0; i < 100; i++) {
      try {
        token = fs.readFileSync(path.join(dataDir, 'control.token'), 'utf8').trim();
        status = await request(controlPort, 'GET', '/status', token);
        if (status.status === 200 && JSON.parse(status.body).error.startsWith('Domain belum')) break;
      } catch { /* server is starting */ }
      await new Promise((resolve) => setTimeout(resolve, 100));
    }
    assert.equal(status?.status, 200);
    assert.equal(JSON.parse(status.body).ready, false);
    assert.match(JSON.parse(status.body).error, /^Domain belum/);
    const share = await request(controlPort, 'POST', '/share', token, file);
    assert.equal(share.status, 503);
    assert.match(share.body, /^Domain belum/);
    console.log('Domain tanpa tunnel tidak menghasilkan tautan palsu: lulus');
  } finally {
    child.kill();
    fs.rmSync(dataDir, { recursive: true, force: true });
  }
}

main().catch((error) => { console.error(error); process.exitCode = 1; });
