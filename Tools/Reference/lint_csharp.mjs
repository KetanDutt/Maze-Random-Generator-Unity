/**
 * Fast C# syntax check that does not need Unity, dotnet or Mono.
 *
 * It parses every .cs file below the given folder with the tree-sitter C# grammar and fails when a
 * file contains a syntax error (an unbalanced brace, a broken statement, ...). This catches the most
 * common "the editor would not compile" mistake in a few hundred milliseconds, which makes it
 * suitable for CI and pre-commit hooks.
 *
 * Usage:
 *   npm install --no-save web-tree-sitter@0.22.6 tree-sitter-wasms
 *   node Tools/Reference/lint_csharp.mjs Assets/Code
 *
 * The parser only checks syntax - it does not compile the code and cannot see missing types or
 * wrong API usage. Run the Unity Test Runner for that.
 */
import Parser from 'web-tree-sitter';
import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const root = process.argv[2] || 'Assets/Code';
const skip = ['/Thirdparty/', '/Library/', '/obj/', '/bin/'];

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full, out);
    } else if (entry.name.endsWith('.cs')) {
      out.push(full);
    }
  }
  return out;
}

let grammarPath;
try {
  grammarPath = require.resolve('tree-sitter-wasms/out/tree-sitter-c_sharp.wasm');
} catch (error) {
  console.error('Missing dependency. Run: npm install --no-save web-tree-sitter@0.22.6 tree-sitter-wasms');
  process.exit(2);
}

await Parser.init();
const language = await Parser.Language.load(grammarPath);
const parser = new Parser();
parser.setLanguage(language);

const files = walk(root).filter(file => !skip.some(part => file.includes(part)));
let failures = 0;

for (const file of files) {
  const source = fs.readFileSync(file, 'utf8');
  const tree = parser.parse(source);
  if (!tree.rootNode.hasError) {
    continue;
  }

  failures++;
  const stack = [tree.rootNode];
  let error = null;
  while (stack.length && !error) {
    const node = stack.pop();
    if (node.type === 'ERROR' || node.isMissing) {
      error = node;
      break;
    }
    for (const child of node.children) {
      stack.push(child);
    }
  }

  const line = error ? error.startPosition.row + 1 : 0;
  const context = source.split('\n').slice(Math.max(0, line - 2), line + 1).join('\n    ');
  console.error(`${file}:${line}: syntax error\n    ${context}`);
}

console.log(`${files.length} files checked, ${failures} with syntax errors`);
process.exit(failures ? 1 : 0);
