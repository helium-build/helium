#!/bin/bash -e

npm install
npm run build
node bin/index.js
npm publish
