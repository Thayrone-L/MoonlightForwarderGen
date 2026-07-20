import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { NACP } from '@tootallnate/nacp';
import { buildNsp } from '@tootallnate/hacbrewpack';

const requestPath = process.argv[2];
if (!requestPath) throw new Error('Usage: node generate-nsp.mjs <request.json>');

const { request, moonlightArguments, keyPath } = JSON.parse(await readFile(requestPath, 'utf8'));
if (!/^[0-9a-f]{16}$/i.test(request.titleId))
  throw new Error('Title ID invalido. Ele deve conter 16 caracteres hexadecimais.');
if (!request.moonlightNroPath.startsWith('/'))
  throw new Error('O caminho do Moonlight deve comecar com /.');

const requiredFiles = [
  ['main', resolve(request.templatePath, 'exefs', 'main')],
  ['main.npdm', resolve(request.templatePath, 'exefs', 'main.npdm')],
  ['NintendoLogo.png', resolve(request.templatePath, 'logo', 'NintendoLogo.png')],
  ['StartupMovie.gif', resolve(request.templatePath, 'logo', 'StartupMovie.gif')],
  ['icon', resolve(request.iconPath)],
];

const entries = new Map();
for (const [name, file] of requiredFiles) entries.set(name, new Uint8Array(await readFile(file)));

const nacp = new NACP();
nacp.id = request.titleId;
nacp.title = request.displayName;
nacp.author = request.publisher;
nacp.version = '1.0.0';
nacp.startupUserAccount = 0;
nacp.screenshot = 0;
nacp.logoType = 2;
nacp.logoHandling = 0;

const nroPath = `sdmc:${request.moonlightNroPath}`;
const nextArgv = [nroPath, ...moonlightArguments].join(' ');
const keyText = await readFile(keyPath, 'utf8');
const result = await buildNsp({
  keys: keyText,
  titleId: request.titleId,
  plaintext: true,
  noPatchNacpLogo: true,
  exefs: new Map([
    ['main', entries.get('main')],
    ['main.npdm', entries.get('main.npdm')],
  ]),
  control: new Map([
    ['control.nacp', new Uint8Array(nacp.buffer)],
    ['icon_AmericanEnglish.dat', entries.get('icon')],
  ]),
  logo: new Map([
    ['NintendoLogo.png', entries.get('NintendoLogo.png')],
    ['StartupMovie.gif', entries.get('StartupMovie.gif')],
  ]),
  romfs: {
    nextArgv: new Blob([nextArgv]),
    nextNroPath: new Blob([nroPath]),
  },
});

const outputPath = resolve(request.outputPath);
await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, new Uint8Array(await result.nsp.arrayBuffer()));
process.stdout.write(JSON.stringify({ outputPath, titleId: result.titleId }));
