// Loads the spike page in headless Chromium, clicks, prints DOM text, console errors and failed requests.
const { chromium } = require(process.argv[2]);
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const problems = [];
  page.on("console", (m) => m.type() === "error" && problems.push("console: " + m.text()));
  page.on("pageerror", (e) => problems.push("pageerror: " + e.message));
  page.on("requestfailed", (r) => problems.push("failed: " + r.url()));
  let requests = 0, bytes = 0;
  page.on("response", async (r) => { requests++; bytes += (await r.body().catch(() => Buffer.alloc(0))).length; if (r.status() >= 400) problems.push(`${r.status()}: ${r.url()}`); });
  await page.goto(process.argv[3]);
  await page.waitForSelector("h1", { timeout: 5000 }).catch(() => {});
  console.log("before:", JSON.stringify(await page.innerText("#app")));
  await page.click("text=Clicks");
  await page.click("text=Clicks");
  await page.click("#add");
  console.log("after: ", JSON.stringify(await page.innerText("#app")));
  console.log(problems.length ? problems.join("\n") : "no errors");
  console.log("resource entries", await page.evaluate(() => { const e = performance.getEntriesByType("resource"); return e.length + " files, " + Math.round(e.reduce((s, x) => s + x.decodedBodySize, 0) / 1024) + " KB"; }));
  console.log(`requests ${requests}, ${(bytes / 1024).toFixed(0)} KB uncompressed`);
  await browser.close();
})();
