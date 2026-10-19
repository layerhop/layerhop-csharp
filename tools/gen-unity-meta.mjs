#!/usr/bin/env node
// Writes a Unity .meta file next to every file and folder of the Unity package (src/LayerHop).
// Unity ignores assets without .meta files in immutable (git URL) packages, so they are committed.
// GUIDs are derived from the path (md5 of "layerhop-csharp:<relative path>"), so the output is
// deterministic and re-running only adds metas for new files. Remove the .meta of a deleted file.
//
// Usage: node tools/gen-unity-meta.mjs
import { createHash } from "node:crypto";
import { existsSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const PKG = join(ROOT, "src", "LayerHop");
const SKIP = new Set(["bin", "obj"]);

const guid = (rel) => createHash("md5").update(`layerhop-csharp:${rel}`).digest("hex");

const tail = "  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

function body(name, isDir) {
  if (isDir) return `folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n${tail}`;
  if (name.endsWith(".cs"))
    return `MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n${tail}`;
  if (name.endsWith(".asmdef")) return `AssemblyDefinitionImporter:\n  externalObjects: {}\n${tail}`;
  if (name === "package.json") return `PackageManifestImporter:\n  externalObjects: {}\n${tail}`;
  if (name.endsWith(".xml") || name.endsWith(".md") || name.endsWith(".txt") || name.endsWith(".json"))
    return `TextScriptImporter:\n  externalObjects: {}\n${tail}`;
  return `DefaultImporter:\n  externalObjects: {}\n${tail}`;
}

let written = 0;
function walk(dir) {
  for (const name of readdirSync(dir).sort()) {
    if (name.endsWith(".meta") || name.startsWith(".") || SKIP.has(name)) continue;
    const abs = join(dir, name);
    const isDir = statSync(abs).isDirectory();
    const rel = relative(PKG, abs).split("\\").join("/");
    const meta = `${abs}.meta`;
    if (!existsSync(meta)) {
      writeFileSync(meta, `fileFormatVersion: 2\nguid: ${guid(rel)}\n${body(name, isDir)}`);
      written++;
    }
    if (isDir) walk(abs);
  }
}

walk(PKG);
console.log(`gen-unity-meta: wrote ${written} .meta file(s)`);
