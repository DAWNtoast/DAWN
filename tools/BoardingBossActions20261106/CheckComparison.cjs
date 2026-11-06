const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const { pathToFileURL, fileURLToPath } = require("node:url");
const { chromium } = require(process.argv[2]);

async function main() {
    const review = path.resolve(__dirname, "../../outputs/boarding-boss-actions-20261106/review");
    const browser = await chromium.launch({executablePath:process.argv[3],headless:true,
        args:["--enable-automation","--disable-background-networking","--disable-component-update","--disable-sync"]});
    const errors = [];
    const gifs = new Set();
    let profile;
    let anchors;
    try {
        const cdp = await browser.newBrowserCDPSession();
        const flags = (await cdp.send("Browser.getBrowserCommandLine")).arguments;
        profile = flags.find(flag=>flag.startsWith("--user-data-dir=")).slice("--user-data-dir=".length);
        const page = await browser.newPage({viewport:{width:1280,height:800}});
        page.on("pageerror", error=>errors.push(String(error)));
        await page.goto(pathToFileURL(path.join(review,"index.html")).href);
        anchors = await page.locator("a").evaluateAll(links=>links.map(link=>link.href));
        assert.equal(anchors.length,5);
        for (const url of anchors) {
            assert.equal(new URL(url).protocol,"file:");
            assert.ok((await fs.stat(fileURLToPath(url))).isFile());
        }
        const ids = await page.locator("#who option").evaluateAll(options=>options.map(option=>option.value));
        assert.equal(ids.length,13);
        for (const id of ids) {
            await page.selectOption("#who",id);
            for (const action of ["Attack","Hit","Cast","Guard"]) {
                await page.locator('#actions button[data-id="'+action+'"]').click();
                const pair = await page.locator(".pairs img").evaluateAll(async images=>{
                    await Promise.all(images.map(image=>image.decode()));
                    return images.map(image=>[image.currentSrc,image.naturalWidth,image.naturalHeight]);
                });
                assert.equal(pair.length,2);
                for (const [index,[src,width,height]] of pair.entries()) {
                    assert.deepEqual([width,height],[400,272]);
                    const url = new URL(src);
                    assert.equal(url.protocol,"file:");
                    assert.equal(url.pathname.includes("/source-before/"),index===1);
                    assert.ok(url.pathname.endsWith("/"+id+"/"+action+".gif"));
                    gifs.add(url.pathname);
                }
            }
        }
        assert.equal(gifs.size,104);
        const previous = await page.locator(".pairs img").evaluateAll(images=>images.map(image=>image.src));
        await page.locator("#again").click();
        const replay = await page.locator(".pairs img").evaluateAll(async images=>{
            await Promise.all(images.map(image=>image.decode()));
            return images.map(image=>image.src);
        });
        assert.ok(replay.every((url,index)=>url!==previous[index]));
        assert.equal(new URL(replay[0]).search,new URL(replay[1]).search);
        await page.setViewportSize({width:390,height:780});
        assert.ok(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
        assert.deepEqual(errors,[]);
    } finally {
        await browser.close();
    }
    await assert.rejects(fs.stat(profile),{code:"ENOENT"});
    const report = {identities:13,actionPairs:52,loadedGifFiles:gifs.size,verifiedLocalAnchors:anchors.length,
        gameplayMp4Links:anchors.filter(url=>url.endsWith(".mp4")).length,synchronizedReplay:true,
        mobileWidth:390,javascriptErrors:errors,temporaryProfileRemoved:true};
    await fs.writeFile(path.join(review,"browser-check.json"), JSON.stringify(report,null,2),"utf8");
    console.log(JSON.stringify(report));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
