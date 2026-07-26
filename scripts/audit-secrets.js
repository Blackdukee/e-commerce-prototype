const fs = require('fs');
const path = require('path');

const configPath = process.argv[2] || path.join(__dirname, '..', 'config', 'vendor.config.json');
const manifestPath = path.join(__dirname, 'secret-fields.json');

if (!fs.existsSync(configPath)) {
  console.error(`Config file not found at: ${configPath}`);
  process.exit(1);
}

if (!fs.existsSync(manifestPath)) {
  console.error(`Secret fields manifest not found at: ${manifestPath}`);
  process.exit(1);
}

const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
const secretPaths = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));

const REF_PATTERN = /^ref:(env|vault|aws-ssm):.+$/i;
let violations = [];

function getValuesByPath(obj, jsonPath) {
  const parts = jsonPath.replace(/^\$\./, '').split('.');
  let results = [obj];

  for (const part of parts) {
    let nextResults = [];
    const isArrayMatch = part.endsWith('[*]');
    const key = isArrayMatch ? part.slice(0, -3) : part;

    for (const item of results) {
      if (item && typeof item === 'object' && key in item) {
        const val = item[key];
        if (isArrayMatch && Array.isArray(val)) {
          nextResults.push(...val);
        } else if (!isArrayMatch) {
          nextResults.push(val);
        }
      }
    }
    results = nextResults;
  }

  return results;
}

for (const jsonPath of secretPaths) {
  const values = getValuesByPath(config, jsonPath);
  for (const val of values) {
    if (typeof val === 'string' && val.length > 0) {
      if (!REF_PATTERN.test(val)) {
        violations.push({ path: jsonPath, value: val });
      }
    }
  }
}

if (violations.length > 0) {
  console.error('\n❌ SECRET AUDIT FAILED: Raw secrets or non-compliant reference strings detected!');
  for (const v of violations) {
    console.error(`  - Field '${v.path}' contains raw value '${v.value}'. Expected ref:env:*, ref:vault:*, or ref:aws-ssm:*`);
  }
  process.exit(1);
}

console.log('✅ SECRET AUDIT PASSED: All secret fields use valid ref:* references.');
process.exit(0);
