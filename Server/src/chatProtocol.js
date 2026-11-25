const { MAX_NAME_LENGTH, MAX_CHAT_LENGTH, MAX_CELL, MAX_TOKEN_LENGTH, MAX_ZONE_LENGTH, MAX_COORD } = require('./config');

// 채팅, 월드, 건물, 재화 메시지 (TCP, 한 줄에 JSON 하나)
//   클라 -> 서버  { t: 'join', name, token, world, v } 접속 후 처음 한 번
//                                                  token(로그인 세션 토큰)이 맞으면 그 계정의 닉네임으로 입장하고
//                                                  world(월드 id)로 들어간다. 1:1 채팅, 친구, 재화 알림도 받는다
//                                                  토큰이 없거나 틀리면 name 으로 광장에 들어가 채팅과 구경만 한다
//                                                  v 는 클라이언트가 아는 메시지 버전 (없으면 1). 2 이상이면 아래의
//                                                  players, pos, pong 을 받고 pos, ping 을 보낸다 (positions.js)
//                 { t: 'pos', w, zn, x, y, z, n }  내 위치 (v2). UDP 로 보내는 좌표와 같은 것을 가끔씩 이 연결로도 보낸다
//                                                  w, zn 은 보낼 때 있던 월드와 구역 (지금 있는 곳과 다르면 버린다),
//                                                  n 은 보낸 순서 (받는 쪽이 늦게 온 예전 좌표를 버리는 데 쓴다)
//                 { t: 'ping' }                    연결 확인 (v2, 5초마다). 서버는 pong 으로 답한다
//                 { t: 'enter', world }            월드 이동 (0: 광장, 내 유저 id: 내 마을, 친구 유저 id: 친구 마을)
//                 { t: 'chat', text }              광장 채팅
//                 { t: 'build', id, type, x, y }    지금 있는 월드에 건물 건설 (id 는 지은 클라이언트가 만듦, x, y 는 기준 칸)
//                 { t: 'demolish', id }             건물 철거 (내가 지은 건물만)
//                 { t: 'move', id, x, y }           건물 이동 (내가 지은 건물만)
//                 { t: 'zone', zone }               월드 안에서 구역 이동 ('' 마을, 'field' 필드, 'house' 집 안)
//                 { t: 'gather', node }             필드의 자원 노드를 캠 (node: 노드 종류)
//                 { t: 'exp_start', area }          채집 타이머 시작 (area: 채집 장소 이름)
//                 { t: 'exp_claim' }                끝난 채집 타이머의 보상 수령
//                 { t: 'harvest', id }              건물 수확 (지금 있는 월드의 내 건물, 수확 주기가 지난 것만)
//                 { t: 'discard', item }            장식 아이템 1개 버리기 (item: 장식물 건물 종류)
//                 { t: 'craft', item }              대장간에서 도구 만들기 (item: crafting.json 의 이름, 예: shovel)
//                 { t: 'tile', x, y, tile }         내 마을 바닥 한 칸 바꾸기 (삽이 있어야 함. tile: 'dirt' 흙, 'grass' 잔디로 되돌림)
//   서버 -> 클라  { t: 'chat', name, text }        어느 월드에 있든 모든 접속자에게 전달
//                 { t: 'kick' }                    같은 계정(사용자명)이 다른 곳에서 로그인하거나 접속해 끊김.
//                                                  받은 클라이언트는 다시 접속하지 않고 로그아웃해 로그인 화면으로 돌아간다
//                 { t: 'world', id, name, mine, build }  들어간 월드. name 은 마을 주인 닉네임(광장은 ''),
//                                                  mine 은 내 마을인지, build 는 여기서 건물을 지을 수 있는지
//                 { t: 'buildings', list: [{ id, type, x, y, owner, zone }] }  지금 있는 월드의 건물 전부
//                                                  (zone: '' 마을, 'house' 집 안. 지을 때는 지은 사람이 있던 구역이 된다)
//                                                  (월드에 들어갈 때, 그리고 요청이 거절되어 화면을 다시 맞출 때)
//                 { t: 'build', id, type, x, y, owner }, { t: 'demolish', id }, { t: 'move', id, x, y }
//                                                  같은 월드의 다른 사람이 바꾼 건물 (바꾼 사람에게는 보내지 않음)
//                 { t: 'left', name }              같이 보이던 사람이 다른 월드나 구역으로 가거나 접속을 끊음
//                 { t: 'players', list: [{ name, id, x, y, z, n }] }  (v2) 지금 같은 월드, 같은 구역에 있는 사람들과 그 위치.
//                                                  들어갈 때마다 (입장, 월드 이동, 구역 이동) 보낸다. id 는 유저 id (없으면 0)
//                 { t: 'pos', name, id, x, y, z, n }  (v2) 같이 있는 사람의 위치 (새로 온 사람도 이것으로 나타난다)
//                 { t: 'pong' }                    (v2) ping 의 답
//                 { t: 'res', items: [{ id, n }] }  내 재화 전부 (id: wood, stone, gold ...). 바뀔 때마다 보낸다
//                 { t: 'harvest', all, list: [{ id, remaining }] }  내 건물의 수확 타이머: remaining 초 뒤에 수확할 수 있음
//                                                  (0 이면 지금). all 이 true 면 지금 있는 월드의 내 건물 전부이고
//                                                  (월드에 들어갈 때, 요청이 거절되어 화면을 다시 맞출 때) 목록에 없는 건물은
//                                                  수확할 수 없다. false 면 수확 요청의 결과로 보내는 그 건물 하나
//                 { t: 'exp', area, remaining }    내 채집 타이머: area 에서 remaining 초 뒤에 끝남 (0 이면 보상을 받을 수 있음)
//                                                  area 가 '' 이면 진행 중인 것이 없음. 입장할 때와 시작/수령 요청의 결과로 보낸다
//                 { t: 'notice', text }            안내 (요청이 거절된 이유, 받은 보상 등)
//                 { t: 'dm', sender }              친구(유저 id sender)가 1:1 채팅을 보냄. 그 사람에게만 가고
//                                                  내용은 HTTP 로 받아 간다 (friendRoutes.js)
//                 { t: 'friends' }                 친구 목록, 친구 요청, 친구의 접속 상태가 바뀜 (목록을 다시 받아 가라는 알림)
//                 { t: 'tiles', list: [{ x, y, tile }] }  지금 있는 월드의 바뀐 바닥 칸 전부 (목록에 없는 칸은 잔디)
//                                                  (월드에 들어갈 때, 그리고 바닥 변경이 거절되어 화면을 다시 맞출 때)
//                 { t: 'tile', x, y, tile }        같은 월드의 다른 사람이 바꾼 바닥 한 칸 (바꾼 사람에게는 보내지 않음)

const isId = v => typeof v === 'string' && /^[A-Za-z0-9_-]{1,64}$/.test(v);
const isCell = v => Number.isInteger(v) && Math.abs(v) <= MAX_CELL;
const isWorld = v => Number.isSafeInteger(v) && v >= 0;
const isZone = v => typeof v === 'string' && v.length <= MAX_ZONE_LENGTH && /^[A-Za-z0-9_-]*$/.test(v);
const isName = (v, max) => typeof v === 'string' && v.length > 0 && v.length <= max;
const isCoord = v => Number.isFinite(v) && Math.abs(v) <= MAX_COORD;

// 잘못된 메시지면 null
function parse(line) {
  let m;
  try {
    m = JSON.parse(line);
  } catch {
    return null;
  }
  if (!m) return null;

  if (m.t === 'join' && typeof m.name === 'string' && m.name.length > 0 && m.name.length <= MAX_NAME_LENGTH) {
    const hasToken = typeof m.token === 'string' && m.token.length > 0 && m.token.length <= MAX_TOKEN_LENGTH;
    const v = Number.isInteger(m.v) && m.v > 1 ? m.v : 1;
    return { t: 'join', name: m.name, token: hasToken ? m.token : null, world: isWorld(m.world) ? m.world : null, v };
  }
  if (m.t === 'pos' && isCoord(m.x) && isCoord(m.y) && isCoord(m.z) && isWorld(m.w) && isZone(m.zn)) {
    return { t: 'pos', world: m.w, zone: m.zn, x: m.x, y: m.y, z: m.z, n: Number.isSafeInteger(m.n) && m.n > 0 ? m.n : 0 };
  }
  if (m.t === 'ping') return { t: 'ping' };
  if (m.t === 'enter' && isWorld(m.world)) return { t: 'enter', world: m.world };
  if (m.t === 'chat' && typeof m.text === 'string') {
    const text = m.text.trim();
    if (text.length > 0 && text.length <= MAX_CHAT_LENGTH) return { t: 'chat', text };
  }
  if (m.t === 'build' && isId(m.id) && isId(m.type) && isCell(m.x) && isCell(m.y)) {
    return { t: 'build', id: m.id, type: m.type, x: m.x, y: m.y };
  }
  if (m.t === 'demolish' && isId(m.id)) return { t: 'demolish', id: m.id };
  if (m.t === 'move' && isId(m.id) && isCell(m.x) && isCell(m.y)) return { t: 'move', id: m.id, x: m.x, y: m.y };
  if (m.t === 'zone' && isZone(m.zone)) return { t: 'zone', zone: m.zone };
  if (m.t === 'gather' && isName(m.node, 64)) return { t: 'gather', node: m.node };
  if (m.t === 'exp_start' && isName(m.area, 40)) return { t: 'exp_start', area: m.area };
  if (m.t === 'exp_claim') return { t: 'exp_claim' };
  if (m.t === 'harvest' && isId(m.id)) return { t: 'harvest', id: m.id };
  if (m.t === 'discard' && isId(m.item)) return { t: 'discard', item: m.item };
  if (m.t === 'craft' && isId(m.item)) return { t: 'craft', item: m.item };
  if (m.t === 'tile' && isCell(m.x) && isCell(m.y) && isId(m.tile)) return { t: 'tile', x: m.x, y: m.y, tile: m.tile };
  return null;
}

const toLine = obj => JSON.stringify(obj) + '\n';
const toItems = amounts => Object.entries(amounts).map(([id, n]) => ({ id, n }));

const chat = (name, text) => toLine({ t: 'chat', name, text });
const kick = () => toLine({ t: 'kick' });
const world = info => toLine({ t: 'world', id: info.id, name: info.name, mine: info.mine, build: info.build });
const buildingList = list => toLine({ t: 'buildings', list });
const build = b => toLine({ t: 'build', id: b.id, type: b.type, x: b.x, y: b.y, owner: b.owner, zone: b.zone ?? '' });
const demolish = id => toLine({ t: 'demolish', id });
const move = b => toLine({ t: 'move', id: b.id, x: b.x, y: b.y });
const left = name => toLine({ t: 'left', name });
const resources = amounts => toLine({ t: 'res', items: toItems(amounts) });
const harvest = (timers, all = false) => toLine({ t: 'harvest', all, list: timers });
const expedition = (area, remaining) => toLine({ t: 'exp', area, remaining });
const notice = text => toLine({ t: 'notice', text });
const dm = senderId => toLine({ t: 'dm', sender: senderId });
const friends = () => toLine({ t: 'friends' });
const players = list => toLine({ t: 'players', list });
const position = p => toLine({ t: 'pos', name: p.name, id: p.id, x: p.x, y: p.y, z: p.z, n: p.n });
const pong = () => toLine({ t: 'pong' });
const tileList = list => toLine({ t: 'tiles', list });
const tile = (x, y, type) => toLine({ t: 'tile', x, y, tile: type });

module.exports = {
  parse, chat, kick, world, buildingList, build, demolish, move, left, resources, harvest, expedition, notice, dm, friends,
  players, position, pong, tileList, tile,
};
