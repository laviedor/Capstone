const fs = require('fs');
const path = require('path');
const { DEMOLISH_REFUND_RATE } = require('./config');

// 기획 데이터 (data 폴더). 서버는 이 값으로 비용, 환급, 수확, 채집 보상을 계산한다
//
//   buildings.json, gathering.json, expeditions.json : Unity 프로젝트에서 내보낸 파일 (Unity 메뉴 Tools > Export Server Data)
//     건물 프리팹/BuildingData, 건물 수확(BuildingHarvest), 필드 자원 프리팹, 장식물 목록, 채집 타이머의 시간과 보상
//     (GatheringTimer.cs, GatheringTimerUI.cs 에 적힌 숫자)을 바꾸면 다시 내보내고 서버를 재시작한다
//
//   buildings.json   { 건물 종류(buildingID): { name, width, height, cost, harvest, harvestSeconds, decoration } }
//     cost: 건설 비용, harvest: 한 번 수확할 때 받는 양 { 자원: 수량 }, harvestSeconds: 수확 주기(초)
//     harvest 가 비어 있으면 수확하지 않는 건물 (장식물)
//     decoration: true 면 장식물. 자원 대신 같은 이름의 장식 아이템 1개를 써서 짓고, 철거하면 1개를 돌려받는다
//     interior: true 면 특수 건물. 장식물과 함께 집 안에 놓을 수 있다 (생산 건물은 마을에만)
//     maxPerVillage: 한 마을에 지을 수 있는 최대 개수 (예: 플레이어 집 1)
//     houseOnly: true 면 집 안 전용 장식 (침대·난로 같은 실내 가구). 마을에는 놓을 수 없다
//   gathering.json   { nodes: { 노드 종류(프리팹 이름): { item, amount, gatherSeconds, respawnSeconds, count } },
//                      decorations: [채집 타이머 보상으로 나올 수 있는 장식물 종류] }
//     count: 필드 하나에 있는 그 종류 노드 수 (채집 횟수 제한에 쓴다)
//   expeditions.json { 채집 장소 이름: { seconds: 걸리는 시간, gold: 골드 보상, decorationChance: 장식물이 나올 확률 } }
//   crafting.json    { 제작 아이템: { name, cost, max } }  대장간에서 만드는 도구 (예: 삽). 서버에서 직접 고치는 파일
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
// 제작법 (대장간): crafting.json { 아이템: { name, cost: { 자원: 수량 }, max: 가질 수 있는 최대 개수 } }
//   서버가 직접 관리하는 파일이다 (Unity 에서 내보내지 않음). 클라이언트 CraftingRecipes.cs 의 값과 같게 유지한다
const recipes = readOptional('crafting.json', {});

const isAmounts = v => v && typeof v === 'object' && Object.values(v).every(n => Number.isInteger(n) && n > 0);
const isPositive = n => Number.isFinite(n) && n > 0;

for (const [type, b] of Object.entries(buildings)) {
  const ok = Number.isInteger(b.width) && b.width > 0 && Number.isInteger(b.height) && b.height > 0
    && isAmounts(b.cost) && isAmounts(b.harvest) && Number.isFinite(b.harvestSeconds) && b.harvestSeconds >= 0;
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
for (const [item, r] of Object.entries(recipes)) {
  const ok = isAmounts(r.cost) && Number.isInteger(r.max) && r.max > 0;
  if (!ok) throw new Error(`data/crafting.json: '${item}' 의 값이 올바르지 않습니다`);
}
for (const [area, e] of Object.entries(expeditions)) {
  const ok = isPositive(e.seconds) && Number.isInteger(e.gold) && e.gold >= 0
    && Number.isFinite(e.decorationChance) && e.decorationChance >= 0 && e.decorationChance <= 1;
  if (!ok) throw new Error(`data/expeditions.json: '${area}' 의 값이 올바르지 않습니다`);
}

const own = (table, key) => (Object.hasOwn(table, key) ? table[key] : null);
const get = type => own(buildings, type);

// 수확할 수 있는 건물인지 (수확 주기마다 harvest 만큼 받는다, harvest.js)
const harvestable = type => {
  const b = get(type);
  return !!b && b.harvestSeconds > 0 && Object.keys(b.harvest).length > 0;
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
  ...Object.values(buildings).filter(b => !b.decoration).flatMap(b => [...Object.keys(b.cost), ...Object.keys(b.harvest)]),
  ...Object.values(nodes).map(n => n.item),
])];

module.exports = {
  get,
  has: type => get(type) !== null,
  harvestable,
  refund,
  items,
  decorations,
  decorationRewards,
  node: id => own(nodes, id),
  expedition: area => own(expeditions, area),
  recipe: item => own(recipes, item),
  craftables: Object.keys(recipes),
};
