// Render only this task's local HTML in an isolated, temporary browser context.
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const out = path.resolve(__dirname, '../../outputs/campaign-boss-review-20261027');
(async () => {
  let browser;
  try { browser = await chromium.launch({headless: true}); }
  catch { browser = await chromium.launch({headless: true, channel: 'msedge'}); }
  try {
    const page = await browser.newPage({ viewport: {width: 1440, height: 1100}, deviceScaleFactor: 1 });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(path.join(out, 'Boss完整成长审核.html')).href);
    await page.screenshot({path: path.join(out, '报告预览-结论.png')});
    const check = await page.evaluate(() => ({
      title: document.title,
      defaultRun: document.querySelector('#runFilter').value,
      runSections: document.querySelectorAll('.run').length,
      visibleRuns: [...document.querySelectorAll('.run')].filter(x => !x.hidden).map(x => x.dataset.run),
      currentFirstKillDetails: document.querySelector('[data-run="LearningMiniboss02"]').querySelectorAll('details').length,
      viewportOverflow: document.documentElement.scrollWidth > innerWidth,
      links: [...document.querySelectorAll('a[href]')].map(x => x.getAttribute('href')),
    }));
    const current = page.locator('[data-run="LearningMiniboss02"]');
    await current.locator('h2').scrollIntoViewIfNeeded();
    await page.screenshot({path: path.join(out, '报告预览-首杀节点.png')});
    await current.locator('details').first().evaluate(x => { x.open = true; });
    await current.locator('details').first().scrollIntoViewIfNeeded();
    await page.screenshot({path: path.join(out, '报告预览-完整配置.png')});
    await page.locator('#runFilter').selectOption('all');
    check.allRunVisibleCount = await page.locator('.run').evaluateAll(xs => xs.filter(x => !x.hidden).length);
    await page.locator('#runFilter').selectOption('LearningMiniboss02');
    await page.locator('#search').fill('余烬方舟号');
    check.searchMatches = await current.locator('details').evaluateAll(xs => xs.filter(x => !x.hidden).length);
    check.missingLinks = check.links.filter(x => !/^[a-z]+:/i.test(x) && !fs.existsSync(path.resolve(out, decodeURIComponent(x))));
    check.pageErrors = errors;
    check.passed = check.defaultRun === 'LearningMiniboss02' && check.visibleRuns.length === 1 && check.currentFirstKillDetails === 13 && check.runSections === 10 && check.allRunVisibleCount === 10 && check.searchMatches === 1 && !check.viewportOverflow && !check.missingLinks.length && !errors.length;
    fs.writeFileSync(path.join(out, 'report-verification.json'), JSON.stringify(check, null, 2));
    console.log(JSON.stringify(check));
    if (!check.passed) process.exitCode = 1;
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
