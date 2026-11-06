// Local, headless Chrome checks the actual file:// page. PowerShell owns its temporary profile.
import assert from "node:assert/strict";
import {spawn} from "node:child_process";
import {once} from "node:events";
import {mkdir, writeFile} from "node:fs/promises";
import path from "node:path";
import {fileURLToPath, pathToFileURL} from "node:url";

const root = fileURLToPath(new URL("../../", import.meta.url));
const out = path.join(root, "outputs", "boarding-remaster-20261106");
const evidence = path.join(out, "validation", "review-browser");
const [browser, profile] = process.argv.slice(2);
assert.ok(browser && profile, "Pass the installed Chrome executable and a temporary profile");
const child = spawn(browser, [
    "--headless=new", "--no-first-run", "--no-default-browser-check",
    "--disable-background-networking", "--disable-component-update", "--disable-sync",
    "--disable-breakpad", "--disable-extensions", "--disable-gpu", "--remote-debugging-pipe",
    "--autoplay-policy=no-user-gesture-required", "--window-size=1280,800", "--user-data-dir=" + profile,
    "about:blank",
], {windowsHide:true, stdio:["ignore","ignore","ignore","pipe","pipe"]});
const exited = once(child, "exit");
const waiters = new Map(), requested = new Set(), errors = [];
let sequence = 0, session = "", buffer = "";
child.stdio[4].setEncoding("utf8");
child.stdio[4].on("data", chunk => {
    buffer += chunk;
    while (buffer.includes("\0")) {
        const end = buffer.indexOf("\0");
        const msg = JSON.parse(buffer.slice(0,end));
        buffer = buffer.slice(end+1);
        if (msg.id && waiters.has(msg.id)) {
            const {resolve,reject,timer} = waiters.get(msg.id);
            clearTimeout(timer);
            waiters.delete(msg.id);
            if (msg.error) reject(Error(msg.error.message));
            else resolve(msg.result);
        } else if (msg.method === "Network.requestWillBeSent") {
            requested.add(msg.params.request.url);
        } else if (msg.method === "Runtime.exceptionThrown") {
            errors.push(msg.params.exceptionDetails.text);
        }
    }
});
function call(method, params={}, sessionId=session) {
    const id = ++sequence;
    return new Promise((resolve,reject) => {
        const timer = setTimeout(() => {waiters.delete(id); reject(Error("Chrome timed out: " + method))}, 10000);
        waiters.set(id,{resolve,reject,timer});
        const command = {id,method,params};
        if (sessionId) command.sessionId = sessionId;
        child.stdio[3].write(JSON.stringify(command)+"\0");
    });
}
async function evaluate(expression) {
    const result = await call("Runtime.evaluate", {expression,returnByValue:true,awaitPromise:true});
    if (result.exceptionDetails) throw Error(result.exceptionDetails.text);
    return result.result.value;
}
async function until(expression) {
    const deadline = Date.now()+8000;
    while (Date.now()<deadline) {
        if (await evaluate(expression)) return;
        await new Promise(resolve=>setTimeout(resolve,80));
    }
    throw Error("Page never reached: " + expression);
}
try {
    const {targetInfos} = await call("Target.getTargets", {}, undefined);
    const target = targetInfos.find(item=>item.type==="page");
    const attached = await call("Target.attachToTarget",{targetId:target.targetId,flatten:true},undefined);
    session=attached.sessionId;
    await Promise.all([call("Page.enable"),call("Runtime.enable"),call("Network.enable")]);
    await call("Emulation.setDeviceMetricsOverride",{width:1280,height:800,deviceScaleFactor:1,mobile:false});
    await call("Page.navigate",{url:pathToFileURL(path.join(out,"index.html")).href});
    await until("document.querySelectorAll('#gallery img').length===5 && [...document.querySelectorAll('#gallery img')].every(i=>i.complete&&i.naturalWidth>0)");
    const initial = [...requested].filter(url=>url.endsWith(".gif"));
    assert.equal(initial.length,5);
    assert.ok(initial.every(url=>url.includes("/gifs/captain/")));
    const {data} = await call("Page.captureScreenshot",{format:"png",fromSurface:true});
    await mkdir(evidence,{recursive:true});
    await writeFile(path.join(evidence,"characters.png"),Buffer.from(data,"base64"));
    await evaluate("byId('first').value='crew_gray_gull_cook_pepe';byId('first').dispatchEvent(new Event('change'));byId('second').value='enemy_party_left';byId('second').dispatchEvent(new Event('change'));document.querySelector('[data-action=Hit]').click()");
    await until("document.querySelectorAll('#gallery img').length===2 && [...document.querySelectorAll('#gallery img')].every(i=>i.complete&&i.naturalWidth>0)");
    assert.ok(await evaluate("[...document.querySelectorAll('#gallery img')].every(i=>i.src.endsWith('/Hit.gif'))"));
    await evaluate("document.querySelector('#actorDetail a').click()");
    assert.ok(await evaluate("document.querySelector('#gallery img').src.endsWith('/Cast.gif')"));
    await evaluate("byId('first').value='crew_salt_crown_diver_isha';byId('first').dispatchEvent(new Event('change'));document.querySelector('#actorDetail a').click()");
    assert.ok(await evaluate("document.querySelector('#gallery img').src.endsWith('/Attack.gif')"));
    await evaluate("byId('first').value='enemy_imperial';byId('first').dispatchEvent(new Event('change'));document.querySelector('[data-action=Death]').click()");
    await until("document.querySelector('#gallery img').src.includes('/named-enemies/enemy_imperial/Death.gif') && document.querySelector('#gallery img').naturalWidth===400");
    await evaluate("byId('tab-fx').click()");
    await until("byId('fxReview').complete&&byId('fxReview').naturalWidth>0&&byId('gallery').childElementCount===0");
    const scenes = await evaluate("data.scenes.length");
    if (scenes) {
        await evaluate("byId('tab-scenes').click();byId('movie').play()");
        await until("byId('movie').readyState>=2 && byId('movie').videoWidth>0");
    }
    await evaluate("byId('tab-actors').click()");
    await call("Emulation.setDeviceMetricsOverride",{width:390,height:780,deviceScaleFactor:1,mobile:false});
    assert.ok(await evaluate("document.documentElement.scrollWidth<=window.innerWidth"));
    assert.deepEqual(errors,[]);
    const result={defaultGifRequests:initial.length, actorComparison:true,namedEnemy:true,fx:true,
        scenePlayback:Boolean(scenes),mobileWidth:390,jsErrors:errors,network:"file:// only"};
    assert.ok([...requested].every(url=>url.startsWith("file:")||url==="about:blank"||url.startsWith("data:")));
    await writeFile(path.join(evidence,"browser-check.json"),JSON.stringify(result,null,2));
    console.log(JSON.stringify(result));
} finally {
    try { await call("Browser.close",{},null); } catch { child.kill(); }
    await Promise.race([exited,new Promise(resolve=>setTimeout(resolve,1200))]);
    if(child.exitCode===null)child.kill();
}
