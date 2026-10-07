import test from 'node:test';
import assert from 'node:assert/strict';

globalThis.document={body:{dataset:{bsVersion:'v44.2D',bsEnvironment:'TEST',bsApiBase:'/api',bsRealtimeHub:'/hubs/command-center'}}};
globalThis.window={location:{origin:'http://localhost'}};
globalThis.localStorage={getItem(){return null;},setItem(){}};

const { parseSignalRFrames } = await import('../../src/BankSwitch.Admin/wwwroot/command-center/assets/js/core/realtime.js');

test('parses multiple SignalR JSON frames and preserves remainder',()=>{
  const RS='\u001e';
  const input=JSON.stringify({type:1,target:'overview',arguments:[{metrics:{tpsLast60Seconds:42}}]})+RS+JSON.stringify({type:6})+RS+'{"type":1';
  const parsed=parseSignalRFrames(input);
  assert.equal(parsed.messages.length,2);
  assert.equal(parsed.messages[0].target,'overview');
  assert.equal(parsed.messages[0].arguments[0].metrics.tpsLast60Seconds,42);
  assert.equal(parsed.messages[1].type,6);
  assert.equal(parsed.remainder,'{"type":1');
});

test('ignores malformed complete frames instead of breaking stream',()=>{
  const RS='\u001e';
  const parsed=parseSignalRFrames('not-json'+RS+JSON.stringify({type:6})+RS);
  assert.deepEqual(parsed.messages,[{type:6}]);
  assert.equal(parsed.remainder,'');
});
