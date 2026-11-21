import csv
import json
import os
import sqlite3
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent
COMMENTS = [
    {"id": "social-001", "plataforma": "foro-demo", "texto": "El envío fue rápido.", "id_producto": 501},
    {"id": "social-002", "plataforma": "foro-demo", "texto": "El soporte resolvió mi consulta.", "id_producto": 502},
    {"id": "social-003", "plataforma": "foro-demo", "texto": "Me gustaría otra presentación.", "id_producto": 503},
]


def rows(filename):
    with (ROOT / filename).open(encoding="utf-8-sig", newline="") as file:
        return list(csv.DictReader(file))


def seed():
    pending = ROOT / "resenas-temporal.db"
    pending.unlink(missing_ok=True)
    reviews = rows("resena_web.csv")
    review_ids = {row["id_opinion"] for row in reviews}
    with closing(sqlite3.connect(pending)) as db, db:
        db.executescript("""
            PRAGMA foreign_keys = ON;
            CREATE TABLE opinion (
                id_opinion INTEGER PRIMARY KEY, id_producto INTEGER NOT NULL,
                fecha_opinion TEXT NOT NULL, texto TEXT NOT NULL, calificacion INTEGER
            );
            CREATE TABLE resena_web (
                id_opinion INTEGER PRIMARY KEY REFERENCES opinion(id_opinion),
                sitio_web TEXT NOT NULL, titulo_resena TEXT,
                compra_verificada INTEGER NOT NULL, votos_utiles INTEGER NOT NULL
            );
        """)
        db.executemany("""
            INSERT INTO opinion VALUES (:id_opinion, :id_producto, :fecha_opinion, :texto, :calificacion)
        """, (o for o in rows("opinion.csv") if o["id_opinion"] in review_ids))
        db.executemany("""
            INSERT INTO resena_web VALUES (
                :id_opinion, :sitio_web, :titulo_resena, :compra_verificada, :votos_utiles
            )
        """, reviews)
    os.replace(pending, ROOT / "resenas.db")


class CommentsHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path != "/comentarios":
            self.send_error(404)
            return
        body = json.dumps(COMMENTS, ensure_ascii=False).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


if __name__ == "__main__":
    with ThreadingHTTPServer(("127.0.0.1", 8765), CommentsHandler) as server:
        seed()
        print("API de demostración disponible en http://127.0.0.1:8765/comentarios", flush=True)
        server.serve_forever()
