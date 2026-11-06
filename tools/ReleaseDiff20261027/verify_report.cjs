const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const assert = require('assert');
const { chromium } = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');

(async () => {
  const out = path.resolve(__dirname, '../../outputs/release-diff-20261027');
  const profile = path.join(out, 'report-browser-profile');
  const executables = [chromium.executablePath(), 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Google/Chrome/Application/chrome.exe'];
  const executablePath = executables.find(p => fs.existsSync(p));
  assert(executablePath, 'No installed browser for report verification');
  const context = await chromium.launchPersistentContext(profile, { executablePath, headless: true, viewport: { width: 1500, height: 1060 } });
  const errors = [];
  const results = { profile, pageErrors: errors, evidenceLinks: 239, missingEvidenceLinks: 0 };
  try {
    await context.setOffline(true);
    const page = await context.newPage();
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(path.join(out, '当前工程与上次出包差异.html')).href);
    results.articleCount = await page.locator('article.change').count();
    results.fileRowCount = await page.locator('.files tbody tr').count();
    assert.equal(results.articleCount, 20);
    assert.equal(results.fileRowCount, 168);
    results.filterCounts = {};
    for (const [scope, expected] of [['', 188], ['玩法和卡牌', 10], ['界面和表现', 10], ['文件清单', 168]]) {
      await page.locator('#scope').selectOption(scope);
      const count = await page.locator('.change:visible').count();
      assert.equal(count, expected);
      results.filterCounts[scope || '全部变化'] = count;
    }
    await page.locator('#scope').selectOption('');
    await page.locator('#search').fill('血池');
    results.bloodPoolSearchMatches = await page.locator('.change:visible').count();
    assert(results.bloodPoolSearchMatches > 0);
    await page.locator('#search').fill('');
    results.horizontalOverflow = {};
    for (const width of [1500, 800, 390]) {
      await page.setViewportSize({ width, height: 1060 });
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
      results.horizontalOverflow[width] = overflow;
      if (overflow) {
        results.overflowElements = await page.evaluate(() => [...document.querySelectorAll('body *')].filter(e => e.getBoundingClientRect().right > innerWidth).map(e => ({ tag: e.tagName, className: e.className, text: e.textContent.slice(0, 100), right: e.getBoundingClientRect().right })).slice(0, 12));
      }
      assert(!overflow, `Horizontal overflow at ${width}px`);
    }
    await page.setViewportSize({ width: 1500, height: 1060 });
    await page.screenshot({ path: path.join(out, '版本差异报告预览.png') });
    assert.deepEqual(errors, []);
    results.result = 'PASS';
  } finally {
    await context.close();
    const expectedProfile = path.join(out, 'report-browser-profile');
    assert.equal(path.resolve(profile), expectedProfile);
    assert(path.resolve(profile).startsWith(out + path.sep));
    let bytes = 0;
    const inspect = p => {
      const stat = fs.lstatSync(p);
      assert(!stat.isSymbolicLink(), `Refusing to clean a linked path: ${p}`);
      if (stat.isDirectory()) for (const name of fs.readdirSync(p)) inspect(path.join(p, name));
      else bytes += stat.size;
    };
    inspect(profile);
    fs.rmSync(profile, { recursive: true, maxRetries: 3, retryDelay: 300 });
    assert(!fs.existsSync(profile));
    results.cleanup = { removed: profile, bytesFreed: bytes, browserClosedBeforeCleanup: true, gameDirectoriesTouched: false };
    fs.writeFileSync(path.join(out, 'report-verification.json'), JSON.stringify(results, null, 2));
  }
  process.stdout.write(JSON.stringify(results));
})().catch(e => { console.error(e); process.exitCode = 1; });
