import https from 'node:https';
import http from 'node:http';
import { readFile } from 'node:fs/promises';

const requestPath = process.argv[2];
if (!requestPath) throw new Error('Usage: node get-moonlight-applist.mjs <request.json>');

const request = JSON.parse(await readFile(requestPath, 'utf8'));
const cert = await readFile(request.certificatePath);
const key = await readFile(request.privateKeyPath);
const agent = new https.Agent({ cert, key, rejectUnauthorized: false });

function get(module, options) {
  return new Promise((resolve, reject) => {
    const call = module.get({ host: request.address, timeout: 12000, ...options }, response => {
      let body = '';
      response.setEncoding('utf8');
      response.on('data', chunk => body += chunk);
      response.on('end', () => response.statusCode >= 200 && response.statusCode < 300
        ? resolve(body)
        : reject(new Error(`Moonlight endpoint returned HTTP ${response.statusCode}.`)));
    });
    call.on('timeout', () => call.destroy(new Error('Moonlight endpoint timed out.')));
    call.on('error', reject);
  });
}

const uniqueId = '0123456789ABCDEF';
// Moonlight descobre a porta HTTPS usando serverinfo via HTTP (porta GameStream padrao 47989).
const serverInfo = await get(http, { port: 47989, path: `/serverinfo?uniqueid=${uniqueId}` });
const portMatch = serverInfo.match(/<HttpsPort>(\d+)<\/HttpsPort>/i);
const httpsPort = portMatch ? Number(portMatch[1]) : 47984;
const appList = await get(https, { port: httpsPort, path: `/applist?uniqueid=${uniqueId}`, agent });
process.stdout.write(JSON.stringify({ appList }));
