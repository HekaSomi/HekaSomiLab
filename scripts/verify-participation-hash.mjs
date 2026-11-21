// Independent JavaScript port of the v1 instrument digest for web contract checks.
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
const file = process.argv[2] ?? 'samples/participation/hash-fixture.json';
const { test, expectedHash } = JSON.parse(readFileSync(file, 'utf8'));
const fields = ['hekasomi.test-hash/1', test.schema, test.id, test.version, test.language, test.title, String(test.items.length)];
for (const q of test.items) fields.push(q.id, q.question, String(q.options.length), ...q.options);
const digest = createHash('sha256');
for (const field of fields) {
  const bytes = Buffer.from(field, 'utf8');
  const length = Buffer.alloc(4); length.writeUInt32BE(bytes.length);
  digest.update(length); digest.update(bytes);
}
const actual = digest.digest('hex');
if (actual !== expectedHash) throw new Error(`Instrument hash mismatch: ${actual} != ${expectedHash}`);
console.log(`JavaScript/C# hash parity passed: ${actual}`);
