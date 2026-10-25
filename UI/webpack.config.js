const path = require("path");
const webpack = require("webpack");
const TerserPlugin = require("terser-webpack-plugin");
const mod = require("./mod.json");
const banner = [
  "Cities: Skylines II UI Module",
  "",
  `Id: ${mod.id}`,
  `Author: ${mod.author}`,
  `Version: ${mod.version}`,
  `Dependencies: ${mod.dependencies.join(",")}`,
].join("\n");

module.exports = {
  mode: "production",
  entry: {
    [mod.id]: "./src/index.tsx"
  },
  externalsType: "window",
  externals: {
    react: "React",
    "cs2/modding": "cs2/modding",
    "cs2/api": "cs2/api",
    "cs2/bindings": "cs2/bindings",
    "cs2/input": "cs2/input",
    "cs2/ui": "cs2/ui",
    "cs2/l10n": "cs2/l10n"
  },
  module: {
    rules: [
      {
        test: /\.tsx?$/,
        use: "ts-loader",
        exclude: /node_modules/
      },
      {
        test: /\.svg$/,
        type: "asset/resource"
      }
    ]
  },
  resolve: {
    extensions: [".tsx", ".ts", ".js"],
    modules: ["node_modules", path.join(__dirname, "src")],
    alias: {
      "mod.json": path.resolve(__dirname, "mod.json")
    }
  },
  output: {
    path: path.resolve(__dirname, "build"),
    filename: "[name].mjs",
    clean: true,
    library: {
      type: "module"
    },
    publicPath: "coui://ui-mods/"
  },
  experiments: {
    outputModule: true
  },
  optimization: {
    minimize: true,
    minimizer: [
      new TerserPlugin({ extractComments: false })
    ]
  },
  plugins: [
    new webpack.BannerPlugin({
      banner,
      stage: webpack.Compilation.PROCESS_ASSETS_STAGE_REPORT,
    })
  ]
};
