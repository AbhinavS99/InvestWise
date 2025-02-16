from contextlib import asynccontextmanager
from app.db import database
from fastapi import FastAPI

@asynccontextmanager
async def lifespan(app: FastAPI):
    await database.connect()
    yield
    await database.disconnect()

app = FastAPI(
    title="InvestWise Dashboard Microservice",
    description="API for InvestWise Dashboard.",
    version="1.0.0",
    lifespan=lifespan
)

app = FastAPI()
@app.get("/api/dashboard/health")
def health_check():
    return {"status": "healthy"}