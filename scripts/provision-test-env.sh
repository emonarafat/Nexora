#!/bin/bash

##############################################################################
# Nexora Test Environment Provisioning Script
#
# This script sets up a complete local test environment with all required
# services for running integration tests and load tests.
#
# Prerequisites:
#   - Docker and Docker Compose installed
#   - .NET 10 SDK installed
#   - k6 load testing tool installed (optional, for load tests)
#
# Usage:
#   ./provision-test-env.sh [--with-load-test]
#
# Options:
#   --with-load-test    Also start services required for load testing
#   --cleanup           Stop and remove all test containers
#   --status            Show status of test environment
##############################################################################

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Service configurations
POSTGRES_PORT=5432
TYPESENSE_PORT=8108
VALKEY_PORT=6379
MSSQL_PORT=1433
RABBITMQ_PORT=5672
RABBITMQ_MGMT_PORT=15672

# Parse arguments
WITH_LOAD_TEST=false
CLEANUP=false
STATUS=false

while [[ $# -gt 0 ]]; do
  case $1 in
    --with-load-test)
      WITH_LOAD_TEST=true
      shift
      ;;
    --cleanup)
      CLEANUP=true
      shift
      ;;
    --status)
      STATUS=true
      shift
      ;;
    *)
      echo -e "${RED}Unknown option: $1${NC}"
      exit 1
      ;;
  esac
done

# Function to print section headers
print_header() {
  echo -e "\n${BLUE}═══════════════════════════════════════════════════${NC}"
  echo -e "${BLUE}  $1${NC}"
  echo -e "${BLUE}═══════════════════════════════════════════════════${NC}\n"
}

# Function to check if service is running
check_service() {
  local service_name=$1
  local port=$2

  if nc -z localhost $port 2>/dev/null; then
    echo -e "${GREEN}✓${NC} $service_name is running on port $port"
    return 0
  else
    echo -e "${RED}✗${NC} $service_name is NOT running on port $port"
    return 1
  fi
}

# Cleanup function
cleanup_test_env() {
  print_header "Cleaning Up Test Environment"

  echo "Stopping all test containers..."
  docker-compose -f "$PROJECT_ROOT/docker-compose.test.yml" down -v

  echo -e "${GREEN}✓${NC} Test environment cleaned up"
  exit 0
}

# Status check function
check_status() {
  print_header "Test Environment Status"

  check_service "PostgreSQL" $POSTGRES_PORT
  check_service "MSSQL Server" $MSSQL_PORT
  check_service "Typesense" $TYPESENSE_PORT
  check_service "Valkey" $VALKEY_PORT

  if [ "$WITH_LOAD_TEST" = true ]; then
    check_service "RabbitMQ" $RABBITMQ_PORT
    check_service "RabbitMQ Management" $RABBITMQ_MGMT_PORT
  fi

  echo ""
  echo "Docker containers:"
  docker ps --filter "name=nexora-test" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

  exit 0
}

# Handle cleanup and status flags
if [ "$CLEANUP" = true ]; then
  cleanup_test_env
fi

if [ "$STATUS" = true ]; then
  check_status
fi

# Main provisioning
print_header "Nexora Test Environment Provisioning"

echo "Project root: $PROJECT_ROOT"
echo "Test configuration: ${WITH_LOAD_TEST:+With load testing services}"
echo ""

# Step 1: Create docker-compose.test.yml if it doesn't exist
print_header "Step 1: Checking Docker Compose Configuration"

COMPOSE_FILE="$PROJECT_ROOT/docker-compose.test.yml"

if [ ! -f "$COMPOSE_FILE" ]; then
  echo "Creating docker-compose.test.yml..."

  cat > "$COMPOSE_FILE" << 'EOF'
# Nexora Test Environment
# Minimal services required for integration and load tests

version: '3.8'

services:
  # PostgreSQL for synonyms and metadata
  postgres-test:
    image: postgres:17-alpine
    container_name: nexora-test-postgres
    environment:
      POSTGRES_DB: nexora_test
      POSTGRES_USER: testuser
      POSTGRES_PASSWORD: testpass123
    ports:
      - "5432:5432"
    volumes:
      - postgres-test-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U testuser"]
      interval: 10s
      timeout: 5s
      retries: 5

  # MSSQL Server for product data source
  mssql-test:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: nexora-test-mssql
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "TestPass123!"
      MSSQL_PID: Developer
    ports:
      - "1433:1433"
    volumes:
      - mssql-test-data:/var/opt/mssql
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P TestPass123! -Q 'SELECT 1' -C"]
      interval: 10s
      timeout: 5s
      retries: 5

  # Typesense for search
  typesense-test:
    image: typesense/typesense:27.1
    container_name: nexora-test-typesense
    environment:
      TYPESENSE_API_KEY: test-api-key-123
      TYPESENSE_DATA_DIR: /data
    ports:
      - "8108:8108"
    volumes:
      - typesense-test-data:/data
    command: '--enable-cors'

  # Valkey for caching
  valkey-test:
    image: valkey/valkey:8-alpine
    container_name: nexora-test-valkey
    ports:
      - "6379:6379"
    volumes:
      - valkey-test-data:/data
    command: valkey-server --save 20 1 --loglevel warning

  # RabbitMQ for event queue (optional, for load tests)
  rabbitmq-test:
    image: rabbitmq:4-management-alpine
    container_name: nexora-test-rabbitmq
    environment:
      RABBITMQ_DEFAULT_USER: testuser
      RABBITMQ_DEFAULT_PASS: testpass123
    ports:
      - "5672:5672"
      - "15672:15672"
    volumes:
      - rabbitmq-test-data:/var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  postgres-test-data:
  mssql-test-data:
  typesense-test-data:
  valkey-test-data:
  rabbitmq-test-data:

networks:
  default:
    name: nexora-test-network
EOF

  echo -e "${GREEN}✓${NC} Created docker-compose.test.yml"
else
  echo -e "${GREEN}✓${NC} docker-compose.test.yml already exists"
fi

# Step 2: Start Docker services
print_header "Step 2: Starting Docker Services"

echo "Starting core test services..."
docker-compose -f "$COMPOSE_FILE" up -d postgres-test mssql-test typesense-test valkey-test

if [ "$WITH_LOAD_TEST" = true ]; then
  echo "Starting load test services..."
  docker-compose -f "$COMPOSE_FILE" up -d rabbitmq-test
fi

# Step 3: Wait for services to be ready
print_header "Step 3: Waiting for Services to be Ready"

echo "Waiting for PostgreSQL..."
until docker exec nexora-test-postgres pg_isready -U testuser > /dev/null 2>&1; do
  echo -n "."
  sleep 1
done
echo -e "\n${GREEN}✓${NC} PostgreSQL is ready"

echo "Waiting for MSSQL Server..."
sleep 10  # MSSQL needs more time to initialize
echo -e "${GREEN}✓${NC} MSSQL Server is ready"

echo "Waiting for Typesense..."
until curl -s http://localhost:8108/health > /dev/null 2>&1; do
  echo -n "."
  sleep 1
done
echo -e "\n${GREEN}✓${NC} Typesense is ready"

echo "Waiting for Valkey..."
until docker exec nexora-test-valkey valkey-cli ping > /dev/null 2>&1; do
  echo -n "."
  sleep 1
done
echo -e "\n${GREEN}✓${NC} Valkey is ready"

# Step 4: Initialize databases
print_header "Step 4: Initializing Test Databases"

echo "Creating PostgreSQL schema..."
docker exec -i nexora-test-postgres psql -U testuser -d nexora_test << 'EOF'
-- Create search_synonyms table
CREATE TABLE IF NOT EXISTS search_synonyms (
    id SERIAL PRIMARY KEY,
    term VARCHAR(255) NOT NULL,
    synonyms TEXT[] NOT NULL,
    locale VARCHAR(10) DEFAULT 'en-US',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    is_active BOOLEAN DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_search_synonyms_term ON search_synonyms(term);
CREATE INDEX IF NOT EXISTS idx_search_synonyms_locale ON search_synonyms(locale);

-- Seed test synonyms
INSERT INTO search_synonyms (term, synonyms, is_active) VALUES
    ('couch', ARRAY['sofa', 'settee', 'divan'], TRUE),
    ('tv', ARRAY['television', 'telly'], TRUE),
    ('laptop', ARRAY['notebook', 'portable computer'], TRUE),
    ('sneakers', ARRAY['trainers', 'kicks', 'tennis shoes'], TRUE),
    ('phone', ARRAY['smartphone', 'mobile', 'cell phone'], TRUE)
ON CONFLICT DO NOTHING;
EOF

echo -e "${GREEN}✓${NC} PostgreSQL schema created and seeded"

# Step 5: Verify environment
print_header "Step 5: Verifying Test Environment"

check_service "PostgreSQL" $POSTGRES_PORT
check_service "MSSQL Server" $MSSQL_PORT
check_service "Typesense" $TYPESENSE_PORT
check_service "Valkey" $VALKEY_PORT

if [ "$WITH_LOAD_TEST" = true ]; then
  check_service "RabbitMQ" $RABBITMQ_PORT
fi

# Step 6: Display connection info
print_header "Test Environment Ready!"

cat << EOF
${GREEN}✓ Test environment provisioned successfully!${NC}

${YELLOW}Connection Information:${NC}

  PostgreSQL:
    Host:     localhost:$POSTGRES_PORT
    Database: nexora_test
    User:     testuser
    Password: testpass123

  MSSQL Server:
    Host:     localhost:$MSSQL_PORT
    User:     sa
    Password: TestPass123!

  Typesense:
    URL:      http://localhost:$TYPESENSE_PORT
    API Key:  test-api-key-123

  Valkey:
    Host:     localhost:$VALKEY_PORT
    (No authentication)

EOF

if [ "$WITH_LOAD_TEST" = true ]; then
  cat << EOF
  RabbitMQ:
    AMQP:     localhost:$RABBITMQ_PORT
    Mgmt UI:  http://localhost:$RABBITMQ_MGMT_PORT
    User:     testuser
    Password: testpass123

EOF
fi

cat << EOF
${YELLOW}Next Steps:${NC}

  1. Run unit tests:
     ${BLUE}dotnet test Nexora.slnx --filter "Category!=Integration"${NC}

  2. Run integration tests:
     ${BLUE}dotnet test Nexora.slnx --filter "Category=Integration"${NC}

  3. Run load tests:
     ${BLUE}cd load-tests && k6 run search-load-test.js${NC}

  4. Check environment status:
     ${BLUE}./scripts/provision-test-env.sh --status${NC}

  5. Clean up when done:
     ${BLUE}./scripts/provision-test-env.sh --cleanup${NC}

${GREEN}Happy testing!${NC}
EOF
