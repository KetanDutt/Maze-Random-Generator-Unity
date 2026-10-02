/**
 * Cross reference check for the C# sources - a Unity-free stand-in for the compiler's "does this
 * member exist?" errors.
 *
 * It parses every script with tree-sitter and checks:
 *   1. member accesses on private fields       (_renderer.sharedMaterial, ...)
 *   2. static accesses on project types        (MazeJsonExport.ToJson, MazePalette.Slate, ...)
 *   3. member accesses on local variables       (grid.Width, result.MeshData, ...)
 * against the members actually declared in the project. Unity and BCL types are skipped, inherited
 * members are resolved through the base class chain.
 *
 * Usage:
 *   npm install --no-save web-tree-sitter@0.22.6 tree-sitter-wasms
 *   node Tools/Reference/check_references.mjs Assets/Code
 *
 * Known limitation: members inherited from Unity base classes (MonoBehaviour.transform,
 * ...) are allowed through an explicit list below; if a real typo hides behind one of those names it
 * will not be reported.
 */
import Parser from 'web-tree-sitter';
import fs from 'fs';
import path from 'path';
import { createRequire } from 'module';

const require = createRequire(import.meta.url);
const root = process.argv[2] || 'Assets/Code';
const skip = ['/Thirdparty/', '/Library/', '/obj/', '/bin/'];

// Members that come from Unity base classes / the BCL, not from the project sources.
const inheritedMembers = new Set([
  'transform', 'gameObject', 'name', 'tag', 'hideFlags', 'enabled', 'isActiveAndEnabled',
  'GetComponent', 'GetComponentInChildren', 'GetComponentInParent', 'GetComponents',
  'GetComponentsInChildren', 'GetComponentsInParent', 'TryGetComponent', 'AddComponent',
  'StartCoroutine', 'StopCoroutine', 'StopAllCoroutines', 'Invoke', 'InvokeRepeating',
  'CancelInvoke', 'IsInvoking', 'SendMessage', 'BroadcastMessage', 'CompareTag',
  'ToString', 'Equals', 'GetHashCode', 'GetType', 'MemberwiseClone', 'GetInstanceID',
]);

let grammarPath;
try {
  grammarPath = require.resolve('tree-sitter-wasms/out/tree-sitter-c_sharp.wasm');
} catch (error) {
  console.error('Missing dependency. Run: npm install --no-save web-tree-sitter@0.22.6 tree-sitter-wasms');
  process.exit(2);
}

function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else if (entry.name.endsWith('.cs')) out.push(full);
  }
  return out;
}

await Parser.init();
const language = await Parser.Language.load(grammarPath);
const parser = new Parser();
parser.setLanguage(language);

const files = walk(root).filter(file => !skip.some(part => file.includes(part)));
const trees = new Map();
const types = new Map();          // simple name (lower case) -> { name, members, base, kind, file }
const enumTypes = new Set();

function addMembers(node, members) {
  const body = node.childForFieldName('body') ||
    node.namedChildren.find(child => child.type === 'declaration_list') || node;
  for (const child of body.children) {
    switch (child.type) {
      case 'field_declaration':
      case 'event_field_declaration':
        for (const declarator of child.descendantsOfType('variable_declarator')) {
          const name = declarator.childForFieldName('name') ||
            declarator.namedChildren.find(node => node.type === 'identifier');
          if (name) members.add(name.text);
        }
        break;
      case 'event_declaration': {
        const name = child.childForFieldName('name');
        if (name) members.add(name.text);
        break;
      }
      case 'method_declaration':
      case 'property_declaration':
      case 'constructor_declaration':
      case 'enum_member_declaration': {
        const name = child.childForFieldName('name');
        if (name) members.add(name.text);
        break;
      }
      default:
        break;
    }
  }
}

for (const file of files) {
  const source = fs.readFileSync(file, 'utf8');
  const tree = parser.parse(source);
  trees.set(file, { tree, source });
  for (const declaration of tree.rootNode.descendantsOfType(
    ['class_declaration', 'struct_declaration', 'interface_declaration', 'enum_declaration'])) {
    const name = declaration.childForFieldName('name');
    if (!name) continue;
    const members = new Set();
    addMembers(declaration, members);
    let base = null;
    const bases = declaration.childForFieldName('bases');
    if (bases) {
      const names = bases.namedChildren.filter(node =>
        node.type === 'identifier' || node.type === 'qualified_name');
      if (names.length) base = names[names.length - 1].text.split('.').pop();
    }
    const key = name.text.toLowerCase();
    if (!types.has(key)) {
      types.set(key, { name: name.text, members, base, kind: declaration.type, file });
    }
    if (declaration.type === 'enum_declaration') enumTypes.add(key);
  }
}

function membersOf(typeKey, seen = new Set()) {
  if (!typeKey || seen.has(typeKey)) return null;
  seen.add(typeKey);
  const info = types.get(typeKey);
  if (!info) return null;
  const members = new Set(info.members);
  for (const member of members) members.add(member);
  if (info.base) {
    const baseMembers = membersOf(info.base.toLowerCase(), seen);
    if (baseMembers) for (const member of baseMembers) members.add(member);
  }
  return members;
}

const problems = [];
const fieldCache = new Map();   // class name -> Map(fieldName -> type)

function fieldsOf(declaration) {
  const key = declaration.id;
  if (fieldCache.has(key)) return fieldCache.get(key);
  const fields = new Map();
  for (const field of declaration.descendantsOfType('field_declaration')) {
    const variable = field.namedChildren.find(node => node.type === 'variable_declaration');
    const type = variable ? variable.childForFieldName('type') : null;
    if (!type) continue;
    const simple = type.type === 'generic_name' ? type.namedChildren[0].text : type.text;
    for (const declarator of field.descendantsOfType('variable_declarator')) {
      const name = declarator.childForFieldName('name') ||
        declarator.namedChildren.find(node => node.type === 'identifier');
      if (name) fields.set(name.text, simple.replace(/[?\[\]]/g, ''));
    }
  }
  fieldCache.set(key, fields);
  return fields;
}

function check(target, memberNode, ownerName, context) {
  if (!target || /[<\[\]>]/.test(target)) return;   // generics/arrays: not project types
  const typeKey = target.toLowerCase();
  if (!types.has(typeKey)) return;
  if (enumTypes.has(typeKey)) return;
  const members = membersOf(typeKey);
  if (!members || members.has(memberNode.text) || inheritedMembers.has(memberNode.text)) return;
  const info = types.get(typeKey);
  problems.push(`${context}: ${ownerName} (${info.name}).${memberNode.text} is not a member`);
}

function walkAccess(access, ownerName, context) {
  const target = access.childForFieldName('expression') || access.namedChildren[0];
  const memberNode = access.childForFieldName('name');
  if (!target || !memberNode) return;
  if (target.type === 'identifier') {
    const local = localsInScope(access, target.text);
    if (local) check(local, memberNode, target.text, context);
    else check(target.text, memberNode, target.text, context);   // static access on a type
  } else if (target.type === 'generic_name' || target.type === 'qualified_name') {
    check(target.namedChildren[target.namedChildren.length - 1].text, memberNode, target.text, context);
  }
}

// Names declared as variables/parameters in the enclosing member declaration shadow a type name.
const scopeCache = new Map();
function localsInScope(node, name) {
  let scope = node.parent;
  while (scope && scope.type !== 'method_declaration' && scope.type !== 'constructor_declaration' &&
         scope.type !== 'property_declaration' && scope.type !== 'class_declaration') {
    scope = scope.parent;
  }
  if (!scope) return null;
  let locals = scopeCache.get(scope.id);
  if (!locals) {
    locals = new Map();
    for (const declaration of scope.descendantsOfType('variable_declaration')) {
      const type = declaration.childForFieldName('type');
      if (!type) continue;
      const simple = type.text;
      for (const declarator of declaration.descendantsOfType('variable_declarator')) {
        const declared = declarator.childForFieldName('name') ||
          declarator.namedChildren.find(child => child.type === 'identifier');
        if (declared) locals.set(declared.text, simple);
      }
    }
    for (const parameter of scope.descendantsOfType('parameter')) {
      const type = parameter.childForFieldName('type');
      const name = parameter.childForFieldName('name');
      if (type && name) {
        locals.set(name.text, type.text);
      }
    }
    scopeCache.set(scope.id, locals);
  }
  return locals.has(name) ? locals.get(name) : null;
}

for (const [file, { tree }] of trees) {
  for (const declaration of tree.rootNode.descendantsOfType(['class_declaration', 'struct_declaration'])) {
    const ownerName = declaration.childForFieldName('name')?.text || '?';
    const fields = fieldsOf(declaration);
    for (const access of declaration.descendantsOfType('member_access_expression')) {
      const target = access.childForFieldName('expression') || access.namedChildren[0];
      const memberNode = access.childForFieldName('name');
      if (!target || !memberNode || target.type !== 'identifier') continue;
      const context = `${file}:${access.startPosition.row + 1}`;
      const fieldType = fields.get(target.text);
      if (fieldType && target.text.startsWith('_')) {
        check(fieldType, memberNode, `${ownerName}.${target.text}`, context);
      } else if (!fieldType) {
        walkAccess(access, ownerName, context);
      }
    }
  }
}

console.log(problems.join('\n'));
console.log(`${files.length} files, ${types.size} declared types, ${problems.length} problems`);
process.exit(problems.length ? 1 : 0);
