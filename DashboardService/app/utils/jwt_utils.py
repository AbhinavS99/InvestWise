import jwt
import os
from datetime import datetime, timezone
from fastapi import HTTPException, Header

SECRET_KEY = os.getenv("GATEWAY_SECRET_KEY")

def verify_gateway_token(x_gateway_auth: str = Header(None)):
    """
    Verify that the request comes from the API Gateway by validating the JWT.
    """
    if not x_gateway_auth:
        raise HTTPException(status_code=403, detail="Forbidden: Missing token")

    try:
        payload = jwt.decode(x_gateway_auth, SECRET_KEY, algorithms=["HS256"])
        if payload.get("role") != "gateway":
            raise HTTPException(status_code=403, detail="Forbidden: Invalid token role")
        exp = payload.get("exp")
        if exp and datetime.fromtimestamp(exp, timezone.utc) < datetime.now(timezone.utc):
            raise HTTPException(status_code=403, detail="Forbidden: Token expired")
    
    except jwt.ExpiredSignatureError:
        raise HTTPException(status_code=403, detail="Forbidden: Token expired")
    except jwt.InvalidTokenError:
        raise HTTPException(status_code=403, detail="Forbidden: Invalid token")
