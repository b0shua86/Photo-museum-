// Restore legacy util.is* helpers removed in modern Node, which tfjs-node calls.
// Imported BEFORE @tensorflow/tfjs-node so the patch is in place when it loads.
import { createRequire } from "node:module";
const require = createRequire(import.meta.url);
const util = require("util");
const add = (k, fn) => { if (typeof util[k] !== "function") util[k] = fn; };
add("isNullOrUndefined", (v) => v === null || v === undefined);
add("isNull", (v) => v === null);
add("isUndefined", (v) => v === undefined);
add("isArray", Array.isArray);
add("isObject", (v) => v !== null && typeof v === "object");
add("isString", (v) => typeof v === "string");
add("isNumber", (v) => typeof v === "number");
add("isFunction", (v) => typeof v === "function");
add("isBoolean", (v) => typeof v === "boolean");
add("isPrimitive", (v) => v === null || (typeof v !== "object" && typeof v !== "function"));
add("isBuffer", (v) => Buffer.isBuffer(v));
add("isRegExp", (v) => v instanceof RegExp);
add("isDate", (v) => v instanceof Date);
add("isError", (v) => v instanceof Error);
add("isSymbol", (v) => typeof v === "symbol");
