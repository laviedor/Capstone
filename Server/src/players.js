const { TIMEOUT_MS, KICKED_TTL_MS } = require('./config');

// 접속 중인 플레이어 목록 (사용자명 기준)
// 같은 사용자명이 새 세션으로 들어오면 기존 세션은 끊긴 목록으로 옮김
class Players {
  constructor() {
    this.byName = new Map(); // 사용자명 -> { id, name, address, port, x, y, z, lastSeen }
    this.kicked = new Map(); // 끊긴 세션 ID -> 끊긴 시각
  }

  // 좌표 갱신
  //   { rejected: true }          이미 끊긴 세션이 보낸 경우
  //   { player, prev, kicked }    prev = 이전 기록, kicked = 이번에 끊긴 기존 세션
  update(msg, rinfo, now = Date.now()) {
    if (this.kicked.has(msg.id)) return { rejected: true };

    const prev = this.byName.get(msg.name);
    let kicked = null;
    if (prev && prev.id !== msg.id) {
      this.kicked.set(prev.id, now);
      kicked = prev;
    }

    const player = {
      id: msg.id,
      name: msg.name,
      address: rinfo.address,
      port: rinfo.port,
      x: msg.x,
      y: msg.y,
      z: msg.z,
      lastSeen: now,
    };
    this.byName.set(msg.name, player);
    return { player, prev, kicked };
  }

  // TIMEOUT_MS 이상 소식 없는 플레이어를 제거하고 반환
  prune(now = Date.now()) {
    const removed = [];
    for (const p of this.byName.values()) {
      if (now - p.lastSeen > TIMEOUT_MS) {
        this.byName.delete(p.name);
        removed.push(p);
      }
    }
    for (const [id, at] of this.kicked) {
      if (now - at > KICKED_TTL_MS) this.kicked.delete(id);
    }
    return removed;
  }

  // name을 제외한 플레이어
  others(name) {
    return [...this.byName.values()].filter(p => p.name !== name);
  }

  // 웹 확인용 { 사용자명: { x, y, z, t } }
  toJSON() {
    const out = {};
    for (const p of this.byName.values()) out[p.name] = { x: p.x, y: p.y, z: p.z, t: p.lastSeen };
    return out;
  }
}

module.exports = Players;
