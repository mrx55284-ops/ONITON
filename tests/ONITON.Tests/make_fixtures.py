# Fixtures.cs 의 금고 샘플을 독립 구현(Python cryptography)으로 다시 만드는 스크립트.
# pip install "cryptography>=44"  →  python3 make_fixtures.py   (출력된 hex 를 Fixtures.cs 에 넣는다)
import hashlib, json, struct
from cryptography.hazmat.primitives.kdf.argon2 import Argon2id
from cryptography.hazmat.primitives.ciphers.aead import AESGCM

def make(master, keyfile, plain, salt, nonce, mem=8192, it=1, par=1):
    flags = 1 if keyfile is not None else 0
    pepper = hashlib.sha256(keyfile).digest() if keyfile is not None else None
    key = Argon2id(salt=salt, length=32, iterations=it, lanes=par, memory_cost=mem, ad=None, secret=pepper).derive(master.encode())
    head = b'ONV2' + bytes([flags]) + struct.pack('<III', mem, it, par) + salt      # 49 bytes = AAD
    ct_tag = AESGCM(key).encrypt(nonce, plain, head)
    return head + nonce + ct_tag[-16:] + ct_tag[:-16]                                # header | nonce | tag | ciphertext

plain = json.dumps({"Entries":[{"Id":"1","Site":"예시","Url":"https://example.com","Username":"앨리스","Password":"S3cret!pw","Note":"fixture"}],"Log":[]}, ensure_ascii=False).encode()
print("A:", make("fixture-master-A", None, plain, bytes(range(32)), bytes(range(100,112))).hex())
print("B:", make("fixture-master-B", b"keyfile-bytes-0123456789", plain, bytes(range(50,82)), bytes(range(200,212))).hex())
