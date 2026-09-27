const dgram = require('dgram');
const express = require('express');

const port = 7777;
const positions = {};  // 사용자명 -> 좌표
const clients = {};    // 사용자명 -> 접속 주소

// UDP: 클라이언트가 보내는 "사용자명,x,y,z" 문자열 수신
const udp = dgram.createSocket('udp4');
udp.on('message', (msg, rinfo) => {
  const parts = msg.toString().split(',');
  const [x, y, z] = parts.splice(-3).map(Number);
  const name = parts.join(',');
  const addr = rinfo.address + ':' + rinfo.port;

  // 같은 사용자명이 다른 곳에서 접속하면 기존 접속을 끊음
  const old = clients[name];
  if (old && old.addr !== addr) {
    udp.send('kick', old.port, old.address);
    console.log(name, '중복 접속 - 기존 접속 종료:', old.addr);
  }
  clients[name] = { addr, address: rinfo.address, port: rinfo.port };

  positions[name] = { x, y, z, t: Date.now() };
  console.log(name, positions[name]);
});
udp.bind(port, () => console.log('UDP 서버 실행 :' + port));

// 웹 확인용 (HTTP, 같은 포트 번호의 TCP)
const app = express();
app.get('/', (req, res) => res.sendFile(__dirname + '/index.html'));
app.get('/positions', (req, res) => res.json(positions));
app.listen(port, () => console.log('웹 확인 http://localhost:' + port));
