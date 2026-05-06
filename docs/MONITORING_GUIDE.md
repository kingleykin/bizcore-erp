# BizCore ERP Monitoring Stack Integration

## Overview

The BizCore ERP project now includes a complete monitoring and logging stack with the following components:

- **Loki**: Log aggregation system for centralized log collection
- **Promtail**: Log shipping agent that forwards logs to Loki
- **Prometheus**: Metrics collection and time-series database
- **Grafana**: Visualization platform for logs and metrics

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Microservices                          │
│  ┌─────────────────────────────────────────────────────┐ │
│  │ Gateway API │ Invoice API │ Payment API │ Report API │ │
│  │     Logs + Metrics Export                           │ │
│  └────────────────┬──────────────────┬─────────────────┘ │
└───────────────────┼──────────────────┼──────────────────┘
                    │                  │
            ┌───────▼────────┐    ┌────▼────────┐
            │   Loki (3100)  │    │ Prometheus  │
            │   Promtail     │    │   (9090)    │
            └────────┬───────┘    └────┬────────┘
                     │                 │
                     └─────────┬───────┘
                               │
                        ┌──────▼──────┐
                        │  Grafana    │
                        │  (3000)     │
                        └─────────────┘
```

## Container Services

### 1. **Loki**
- **Port**: 3100
- **Function**: Centralized log aggregation
- **Configuration**: Uses default Loki configuration

### 2. **Promtail**
- **Function**: Log shipping agent
- **Configuration**: `promtail-config.yml`
- **Destination**: Sends logs to Loki

### 3. **Prometheus**
- **Port**: 9090
- **Function**: Metrics collection
- **Configuration**: `prometheus.yml`
- **Scrape Jobs**: Configured for all microservices

### 4. **Grafana**
- **Port**: 3001
- **Default Credentials**: admin/admin
- **Function**: Visualization of logs and metrics

## Accessing the Monitoring Stack

### Grafana Dashboard
- **URL**: `http://localhost:3001`
- **Default Username**: admin
- **Default Password**: admin

### Prometheus Metrics
- **URL**: `http://localhost:9090`
- **Targets**: http://localhost:9090/targets

### Loki Logs
- **API Endpoint**: `http://localhost:3100`
- **Access through Grafana** for visualization

## Microservices Integration

### Enabled Features Per Service

Each microservice (Gateway, Invoice, Payment, Report) is configured with:

1. **Serilog with Loki Sink**
   - Console output for local development
   - Loki HTTP endpoint for centralized logging
   - Service and job labels for filtering

2. **Prometheus Metrics Endpoint**
   - Available at `/metrics` endpoint on each service
   - HTTP request metrics (latency, count, size)
   - Exposed port 8080 internally

3. **Environment Configuration**
   - `Loki__Url`: Automatically set to `http://loki:3100`
   - Fallback to default if not configured

### Service Metrics Endpoints

| Service | Metrics URL |
|---------|-------------|
| Gateway API | http://localhost:5000/metrics |
| Invoice API | http://invoice-api:8080/metrics |
| Payment API | http://payment-api:8080/metrics |
| Report API | http://report-api:8080/metrics |

## NuGet Packages Added

```xml
<!-- Serilog Loki Integration -->
<PackageReference Include="Serilog.Sinks.Grafana.Loki" Version="8.3.0" />

<!-- Prometheus Metrics -->
<PackageReference Include="prometheus-net.AspNetCore" Version="8.2.1" />
```

## Configuration Files

### 1. prometheus.yml
Located at: `./prometheus.yml`

Configures scrape jobs for:
- Gateway API
- Invoice API
- Payment API
- Report API

### 2. promtail-config.yml
Located at: `./promtail-config.yml`

Configured to:
- Use Docker service discovery (`docker_sd_configs`) to automatically detect containers
- Apply relabeling rules to extract service names from container names
- Remove `bizcore-` prefix for cleaner service labels
- Result: logs are labeled with `service=invoice-api`, `service=payment-api`, etc.

## Docker Compose Integration

All services are configured with:
- `Loki__Url` environment variable
- Dependency on `loki` service
- Service-specific labels for log filtering

## Getting Started with Monitoring

### 1. Start the Stack
```bash
docker-compose up -d
```

### 2. Restart Promtail (Important!)
```bash
# After starting the stack, restart Promtail to apply new config
docker-compose restart promtail
```

### 3. Verify Services
```bash
# Check if services are running
docker-compose ps

# Verify Loki is accepting logs
curl -v http://localhost:3100/api/prom/labels

# Check Promtail targets (should show service labels)
curl http://localhost:3100/api/prom/label/service/values
```

### 3. Test Log Filtering in Grafana

After restarting Promtail, you should see logs with proper service labels:

**By Service:**
```
{service="invoice-api"}
{service="payment-api"}
{service="gateway-api"}
{service="report-api"}
```

**By Job:**
```
{job="invoice-api"}
{job="payment-api"}
```

**Combined with Log Level:**
```
{service="payment-api"} |= "error"
```

### 3. Access Grafana
- Open browser to http://localhost:3001
- Login with admin/admin

### 4. Add Data Sources

#### Add Prometheus Data Source
1. Go to Configuration > Data Sources
2. Click "Add data source"
3. Select "Prometheus"
4. URL: `http://prometheus:9090`
5. Click "Save & test"

#### Add Loki Data Source
1. Go to Configuration > Data Sources
2. Click "Add data source"
3. Select "Loki"
4. URL: `http://loki:3100`
5. Click "Save & test"

### 5. Import Dashboards

Sample queries for Grafana:

**Metrics Query (Prometheus)**:
```
rate(http_requests_received_total[5m])
```

**Logs Query (Loki)**:
```
{job="invoice-api"} | json | line_format "{{.message}}"
```

## Log Filtering in Grafana

### By Service
```
{service="invoice-api"}
{service="payment-api"}
{service="gateway-api"}
{service="report-api"}
```

### By Job
```
{job="invoice-api"}
{job="payment-api"}
```

### By Log Level
```
{service="payment-api"} |= "error"
```

### Combined Filters
```
{service="gateway-api", job="gateway-api"} |= "request"
```

### Advanced Queries
```
{service=~".*-api"} |= "error" | json
```

**Note**: The `service` label is automatically extracted from container names (e.g., `bizcore-invoice-api` → `service=invoice-api`)

## Troubleshooting

### Logs Not Appearing in Loki

1. Check if Loki is running:
   ```bash
   docker logs loki
   ```

2. Verify microservice can reach Loki:
   ```bash
   docker exec <service-container> curl http://loki:3100/api/prom/labels
   ```

3. Check service logs:
   ```bash
   docker logs <service-container>
   ```

4. **Important**: Restart Promtail after config changes:
   ```bash
   docker-compose restart promtail
   ```

5. Verify Promtail is discovering services:
   ```bash
   curl http://localhost:3100/api/prom/label/service/values
   ```
   Should return: `["gateway-api", "invoice-api", "payment-api", "report-api"]`

### Prometheus Not Scraping Metrics

1. Verify targets in Prometheus UI: http://localhost:9090/targets
2. Check if services are exposing `/metrics` endpoint
3. Verify service discovery/DNS resolution between containers

### Grafana Data Source Connection Issues

1. Ensure containers are on same Docker network
2. Use service names (loki, prometheus) not localhost
3. Check container logs for connection errors

## Performance Considerations

- **Loki**: Stores logs efficiently with compression
- **Prometheus**: Retention set based on disk space
- **Promtail**: Minimal resource usage, parallel log processing
- **Grafana**: Lightweight visualization layer

## Security Notes

### Default Credentials ⚠️
The default Grafana password (`admin/admin`) should be changed in production.

### Network Access
- Services communicate via Docker internal network
- External access through mapped ports

### Data Retention
- Configure Prometheus retention in `prometheus.yml`
- Configure Loki retention via environment variables

## Next Steps

1. Create custom dashboards for business metrics
2. Set up alerting rules in Prometheus
3. Configure log retention policies
4. Integrate with your incident management system

## Useful Resources

- [Grafana Documentation](https://grafana.com/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Loki Documentation](https://grafana.com/docs/loki/latest/)
- [Serilog Loki Integration](https://github.com/JosephWoodward/Serilog-Sinks-Grafana-Loki)
- [prometheus-net Documentation](https://github.com/prometheus-net/prometheus-net)
