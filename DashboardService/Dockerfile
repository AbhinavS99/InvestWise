# Use the official Python image as the base image
FROM python:3.10-slim

# Set environment variables for Python
ENV PYTHONDONTWRITEBYTECODE 1  # Prevents Python from writing .pyc files
ENV PYTHONUNBUFFERED 1        # Ensures stdout/stderr are flushed

# Set the working directory in the container
WORKDIR /app
RUN apt-get update && apt-get install -y curl

# Copy the requirements file to the container
COPY requirements.txt /app/

# Install the Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy the app directory to the container
COPY ./app /app/app

# Expose the port that the app will run on
EXPOSE 9000

# Define the command to run the application
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "9000"]