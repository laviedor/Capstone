const fs = require('fs');
const path = require('path');
const { DEMOLISH_REFUND_RATE } = require('./config');

// 기획 데이터 (data 폴더). 서버는 이 값으로 비용, 환급, 생산, 채집 보상을 계산한다
//
//   buildings.json, gathering.json, expeditions.json : Unity 프로젝트에서 내보낸 파일 (Unity 메뉴 Tools > Export Server Data)
//     건물 프리팹/BuildingData, 필드 자원 프리팹, 장식물 목록, 채집 타이머의 시간과 보상(GatheringTimer.cs,
//     GatheringTimerUI.cs 에 적힌 숫자)을 바꾸면 다시 내보내고 서버를 재시작한다
//
//   buildings.json   { 건물 종류(buildingID): { name, width, height, cost, production, interval, decoration } }
//     cost, production: { 자원: 수량 }, interval: 생산 주기(초). production 이 비어 있으면 생산하지 않는 건물
//     decoration: true 면 장식물. 자원 대신 같은 이름의 장식 아이템 1개를 써서 짓고, 철거하면 1개를 돌려받는다
//   gathering.json   { nodes: { 노드 종류(프리팹 이름): { item, amount, gatherSeconds, respawnSeconds, count } },
//                      decorations: [채집 타이머 보상으로 나올 수 있는 장식물 종류] }
//     count: 필드 하나에 있는 그 종류 노드 수 (채집 횟수 제한에 쓴다)
//   expeditions.json { 채집 장소 이름: { seconds: 걸리는 시간, gold: 골드 보상, decorationChance: 장식물이 나올 확률 } }
//   자원 이름은 소문자 (wood, stone, gold)
const dataDir = path.join(__dirname, '..', 'data');
const read = file => JSON.parse(fs.readFileSync(path.join(dataDir, file), 'utf8'));
const readOptional = (file, fallback) => {
  try {
    return read(file);
  } catch (err) {
    if (err.code !== 'ENOENT') throw err;
    return fallback; // 파일이 없으면 그 기능만 쓰지 못한다
  }
};

const buildings = read('buildings.json');
const gathering = readOptional('gathering.json', {});
const nodes = gathering.nodes ?? {};
const expeditions = readOptional('expeditions.json', {});

const isAmounts = v => v && typeof v === 'object' && Object.values(v).every(n => Number.isInteger(n) && n > 0);
const isPositive = n => Number.isFinite(n) && n > 0;

for (const [type, b] of Object.entries(buildings)) {
  const ok = Number.isInteger(b.width) && b.width > 0 && Number.isInteger(b.height) && b.height > 0
    && isAmounts(b.cost) && isAmounts(b.production) && Number.isFinite(b.interval);
  if (!ok) throw new Error(`data/buildings.json: '${type}' 의 값이 올바르지 않습니다`);
  if (b.decoration) {
    b.cost = { [type]: 1 };   // 장식 아이템 1개로 짓고
    b.refund = { [type]: 1 }; // 철거하면 그대로 돌려받는다
  }
}
for (const [id, n] of Object.entries(nodes)) {
  const ok = typeof n.item === 'string' && Number.isInteger(n.amount) && n.amount > 0
    && isPositive(n.gatherSeconds) && isPositive(n.respawnSeconds) && Number.isInteger(n.count) && n.count > 0;
  if (!ok) throw new Error(`data/gathering.json: 노드 '${id}' 의 값이 올바르지 않습니다`);
}
for (const [area, e] of Object.entries(expeditions)) {
  const ok = isPositive(e.seconds) && Number.isInteger(e.gold) && e.gold >= 0
    && Number.isFinite(e.decorationChance) && e.decorationChance >= 0 && e.decorationChance <= 1;
  if (!ok) throw new Error(`data/expeditions.json: '${area}' 의 값이 올바르지 않습니다`);
}

const own = (table, key) => (Object.hasOwn(table, key) ? table[key] : null);
const get = type => own(buildings, type);

// 주기적으로 자원을 생산하는 건물인지
const produces = type => {
  const b = get(type);
  return !!b && b.interval > 0 && Object.keys(b.production).length > 0;
};

// 철거하면 돌려받는 것 { 자원: 수량 }
//   클라이언트(C#)의 Mathf.FloorToInt(cost * 0.9f) 와 같은 값이 나오도록 float 로 계산한다
function refund(type) {
  const b = get(type);
  if (!b) return {};
  if (b.refund) return b.refund;

  const out = {};
  for (const [item, amount] of Object.entries(b.cost)) {
    const n = Math.floor(Math.fround(amount * Math.fround(DEMOLISH_REFUND_RATE)));
    if (n > 0) out[item] = n;
  }
  return out;
}

// 장식물 종류 전부 (유저가 가진 장식 아이템 수를 알려 줄 때 쓴다)
const decorations = Object.keys(buildings).filter(type => buildings[type].decoration);
// 채집 타이머 보상으로 나올 수 있는 장식물
const decorationRewards = (gathering.decorations ?? []).filter(type => decorations.includes(type));

// 데이터에 나오는 자원 이름 전부 (장식 아이템은 빼고)
const items = [...new Set([
  ...Object.values(buildings).filter(b => !b.decoration).flatMap(b => [...Object.keys(b.cost), ...Object.keys(b.production)]),
  ...Object.values(nodes).map(n => n.item),
])];

module.exports = {
  get,
  has: type => get(type) !== null,
  produces,
  refund,
  items,
  decorations,
  decorationRewards,
  node: id => own(nodes, id),
  expedition: area => own(expeditions, area),
};
