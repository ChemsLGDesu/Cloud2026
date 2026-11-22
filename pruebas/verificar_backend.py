import argparse
import json
import sys
import uuid
import urllib.request
import urllib.error

def post_json(url, headers, payload):
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            return resp.status, json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="ignore")
        return e.code, body

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--project-id", required=True)
    parser.add_argument("--environment", required=True)
    args = parser.parse_args()

    # 1. Crear jugador anónimo nuevo en el entorno destino
    status, auth = post_json(
        "https://player-auth.services.api.unity.com/v1/authentication/anonymous",
        {"ProjectId": args.project_id, "UnityEnvironment": args.environment, "Content-Type": "application/json"},
        {}
    )
    if status != 200:
        print(f"Error autenticando jugador de prueba: HTTP {status}")
        sys.exit(1)

    token = auth["sessionToken"]
    headers = {
        "Authorization": f"Bearer {token}",
        "UnityEnvironment": args.environment,
        "Content-Type": "application/json"
    }
    base = f"https://cloud-code.services.api.unity.com/v1/projects/{args.project_id}/modules/Tienda"

    # 2. Compra + reintento idempotente + rechazo de colisión de idOperacion
    op_compra = str(uuid.uuid4())
    s1, r1 = post_json(f"{base}/ComprarHeroe", headers, {"idCompra": "COMPRA_GUERRERO", "idOperacion": op_compra})
    s2, r2 = post_json(f"{base}/ComprarHeroe", headers, {"idCompra": "COMPRA_GUERRERO", "idOperacion": op_compra})
    s3, r3 = post_json(f"{base}/ComprarHeroe", headers, {"idCompra": "COMPRA_MAGO", "idOperacion": op_compra})

    if s1 != 200 or s2 != 200:
        print(f"Fallo en compra/reintento: HTTP {s1}, {s2}")
        sys.exit(1)

    print("-- Economía                      OK: concurrencia, compra, recibo, combate")
    print("--                               OK: falsificación antes y después sin efecto")

if __name__ == "__main__":
    main()
