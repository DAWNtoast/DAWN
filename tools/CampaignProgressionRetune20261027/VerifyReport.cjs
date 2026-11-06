// Validate this task's local artifacts in a disposable browser context.
const fs = require('fs');
const path = require('path');
const { pathToFileURL } = require('url');
const { chromium } = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const root = path.resolve(__dirname, '../..');
const out = path.join(root, 'outputs/campaign-progression-retune-20261027');
const qa = path.join(out, 'ReportReview');
fs.mkdirSync(qa, {recursive: true});
(async () => {
  let browser;
  try { browser = await chromium.launch({headless: true}); }
  catch { browser = await chromium.launch({headless: true, channel: 'msedge'}); }
  try {
    const page = await browser.newPage({viewport: {width: 1440, height: 1100}, deviceScaleFactor: 1});
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(pathToFileURL(path.join(out, '成长节奏与Boss校准报告.html')).href);
    await page.screenshot({path: path.join(qa, '最终报告-结论.png')});
    const check = await page.evaluate(() => ({
      title: document.title,
      defaultRun: document.querySelector('#runFilter').value,
      runSections: document.querySelectorAll('.run').length,
      visibleRuns: [...document.querySelectorAll('.run')].filter(x => !x.hidden).map(x => x.dataset.run),
      firstKills: document.querySelector('[data-run="Retune04"]').querySelectorAll('details').length,
      overflowDesktop: document.documentElement.scrollWidth > innerWidth,
      links: [...document.querySelectorAll('a[href]')].map(x => x.getAttribute('href')),
      hasFinalRules: ['×0.25', '每 40 海里', '雷霆 6', '血契 7', '460 → 300', '510 → 320', '第 31 航'].every(x => document.body.innerText.includes(x)),
      unfinished: document.body.innerText.includes('正在封存'),
    }));
    const current = page.locator('[data-run="Retune04"]');
    await current.locator('h2').scrollIntoViewIfNeeded();
    await page.screenshot({path: path.join(qa, '最终报告-首杀金币.png')});
    await current.locator('details').first().evaluate(x => {x.open = true;});
    await current.locator('details').first().scrollIntoViewIfNeeded();
    await page.screenshot({path: path.join(qa, '最终报告-首杀配置.png')});
    await page.locator('#runFilter').selectOption('all');
    check.allRunVisibleCount = await page.locator('.run').evaluateAll(xs => xs.filter(x => !x.hidden).length);
    await page.locator('#runFilter').selectOption('Retune04');
    await page.locator('#search').fill('余烬方舟号');
    check.searchMatches = await current.locator('details').evaluateAll(xs => xs.filter(x => !x.hidden).length);
    await page.locator('#search').fill('');
    await page.setViewportSize({width: 390, height: 844});
    await page.evaluate(() => scrollTo(0, 0));
    check.overflowMobile = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth);
    await page.screenshot({path: path.join(qa, '最终报告-窄屏.png')});
    check.missingLinks = check.links.filter(x => !/^(#|[a-z]+:)/i.test(x) && !fs.existsSync(path.resolve(out, decodeURIComponent(x.split('#')[0]))));
    await page.setViewportSize({width: 1440, height: 1100});
    await page.goto(pathToFileURL(path.join(root, 'outputs/card-list-by-school-20261023/全部卡牌清单.html')).href);
    await page.screenshot({path: path.join(qa, '卡牌清单-解锁等级.png')});
    check.cardList = await page.evaluate(() => ({
      title: document.title,
      levelText: document.body.innerText.slice(0, 2500),
      overflow: document.documentElement.scrollWidth > innerWidth,
    }));
    await page.goto(pathToFileURL(path.join(root, 'outputs/campaign-boss-review-20261027/Boss完整成长审核.html')).href);
    check.previousReportHasLatestLink = await page.locator('#latest-progression-report a').count() === 1;
    await page.screenshot({path: path.join(qa, '原报告-新版入口.png')});
    check.pageErrors = errors;
    check.passed = check.defaultRun === 'Retune04' && check.visibleRuns.join(',') === 'Retune04' && check.firstKills === 13 && check.runSections === 3 && check.allRunVisibleCount === 3 && check.searchMatches === 1 && check.hasFinalRules && !check.unfinished && !check.overflowDesktop && !check.overflowMobile && !check.missingLinks.length && !errors.length && check.previousReportHasLatestLink && !check.cardList.overflow;
    fs.writeFileSync(path.join(qa, 'report-verification.json'), JSON.stringify(check, null, 2));
    console.log(JSON.stringify(check));
    if (!check.passed) process.exitCode = 1;
  } finally { await browser.close(); }
})().catch(e => {console.error(e); process.exitCode = 1;});
