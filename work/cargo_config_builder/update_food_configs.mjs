// Compatibility entry point retained for older maintenance notes.
// Food unification is complete; standard route rebuilding now owns these CSV files.
console.warn("update_food_configs.mjs is deprecated; running expand_standard_routes.mjs instead.");
await import("./expand_standard_routes.mjs");
