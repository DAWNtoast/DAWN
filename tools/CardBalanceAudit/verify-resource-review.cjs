'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL, fileURLToPath } = require('node:url');
const { chromium } = require('C:/Users/Admin/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async () => {
  const out = process.argv[2] ? path.resolve(process.argv[2]) : path.resolve(__dirname, '../../Artifacts/CardSchoolRebalance20261012');
  const url = pathToFileURL(path.join(out, 'index.html')).href;
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 1050 } });
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await page.goto(url);
    await page.locator('#cardRows tr').first().waitFor();
    if (await page.locator('#cardRows tr').count() !== 218) throw Error('Not all cards rendered');
    await page.locator('#cardSchool').selectOption('fire');
    await page.locator('#cardRank').selectOption('3');
    if (await page.locator('#cardRows tr').count() !== 22) throw Error('School/rank filter failed');
    await page.locator('#query').fill('火把突击');
    if (await page.locator('#cardRows tr').count() !== 1) throw Error('Card search failed');
    const example = await page.locator('#cardRows tr').innerText();
    if (!example.includes('火药') || !example.includes('弱效果')) throw Error('Fallback costs missing');
    await page.locator('#query').fill('');
    await page.locator('#cardSchool').selectOption('');
    await page.locator('#cardRank').selectOption('0');
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.screenshot({ path: path.join(out, 'review-overview.png'), fullPage: false });
    await page.locator('#rank').selectOption('3');
    await page.locator('#target').selectOption('training_target_3');
    if (await page.locator('#battleRows tr').count() !== 7) throw Error('Seven-school comparison missing');
    await page.locator('#battleRows').scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, 'review-consumables.png'), fullPage: false });
    await page.locator('#supply').selectOption('none');
    const zeroText = await page.locator('#battleRows').innerText();
    let endurance = null;
    if (await page.locator('#endRows').count()) {
      if (await page.locator('#endRows tr').count() !== 38 || await page.locator('#endBestRows tr').count() !== 7) throw Error('Endurance default rows missing');
      await page.locator('#endRows button').first().click();
      const recipe = await page.locator('#endRecipeBody').innerText();
      if (!recipe.includes('准备') || !recipe.includes('海战') || !recipe.includes('接舷') || recipe.includes('card_')) throw Error('Endurance card recipe lookup failed');
      await page.locator('#endSchool').selectOption('fire');
      if (await page.locator('#endRows tr').count() !== 6) throw Error('Endurance school filter failed');
      await page.locator('#endSchool').selectOption('');
      await page.locator('#endCohort').selectOption('sources');
      if (await page.locator('#endRows tr').count() !== 140) throw Error('Endurance source rows missing');
      await page.locator('#endRank').selectOption('1');
      if (!(await page.locator('#endRows').innerText()).includes('没有此强化等级')) throw Error('Untested source rank shown as measured');
      await page.locator('#endCohort').selectOption('baseline');
      await page.locator('#endRank').selectOption('3');
      await page.locator('#endBestPanel').scrollIntoViewIfNeeded();
      await page.screenshot({ path: path.join(out, 'review-endurance.png'), fullPage: false });
      endurance = { baselineDecks: 38, sourcesAndControls: 140, schoolFilter: 6, bestFixedRows: 7, recipeVerified: true, untestedRanksOmitted: true };
      const links = await page.locator('a[href]').evaluateAll(as => as.map(a => a.getAttribute('href')).filter(h => !h.startsWith('#')));
      for (const href of links) { const target = new URL(href, url); if (target.protocol === 'file:' && !fs.existsSync(fileURLToPath(target))) throw Error('Broken review link: ' + href); }
      if (/undefined|NaN/.test(await page.locator('body').innerText())) throw Error('Invalid number or missing text in review');
    }
    await page.setViewportSize({ width: 420, height: 900 });
    await page.goto(url);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
    if (overflow) throw Error('Document overflows narrow viewport');
    if (errors.length) throw Error(errors.join('\n'));
    fs.writeFileSync(path.join(out, 'report-ui-check.json'), JSON.stringify({ passed: true, cardsRendered: 218, schoolFilter: 22, search: 1, endurance, desktop: '1440x1050', mobile: '420x900', pageErrors: errors, zeroSupplyVisible: zeroText }, null, 2));
    console.log('Review UI checks passed; desktop screenshots saved.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
