param environment string
param location string
param applicationInsightsId string
param alertEmail string
param tags object

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'remito-${environment}-alerts'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'remito-${environment}'
    enabled: !empty(alertEmail)
    emailReceivers: empty(alertEmail) ? [] : [
      {
        name: 'Remito operations'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

var actionGroups = empty(alertEmail) ? [] : [
  actionGroup.id
]

resource fiveHundredAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: 'remito-${environment}-api-5xx-rate'
  location: location
  tags: tags
  properties: {
    displayName: 'Remito ${environment}: API 5xx rate exceeds 5%'
    description: 'Fires when failed API requests exceed 5 percent in a five-minute window.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    targetResourceTypes: [
      'microsoft.insights/components'
    ]
    criteria: {
      allOf: [
        {
          query: 'requests | summarize total=count(), failed=countif(success == false) by bin(timestamp, 5m) | where total > 0 | extend errorRate=100.0 * failed / total | project errorRate'
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'errorRate'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroups
    }
  }
}

resource latencyAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: 'remito-${environment}-api-p95-latency'
  location: location
  tags: tags
  properties: {
    displayName: 'Remito ${environment}: API P95 latency exceeds 400 ms'
    description: 'Fires when request P95 latency exceeds 400 milliseconds in a five-minute window.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    targetResourceTypes: [
      'microsoft.insights/components'
    ]
    criteria: {
      allOf: [
        {
          query: 'requests | summarize p95LatencyMs=percentile(duration, 95) by bin(timestamp, 5m) | project p95LatencyMs'
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'p95LatencyMs'
          operator: 'GreaterThan'
          threshold: 400
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroups
    }
  }
}

resource rateFreshnessAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: 'remito-${environment}-rate-freshness'
  location: location
  tags: tags
  properties: {
    displayName: 'Remito ${environment}: rate freshness exceeds 15 minutes'
    description: 'Fires when the rate_freshness_seconds custom metric exceeds 900 seconds.'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    scopes: [
      applicationInsightsId
    ]
    targetResourceTypes: [
      'microsoft.insights/components'
    ]
    criteria: {
      allOf: [
        {
          query: 'customMetrics | where name == "rate_freshness_seconds" | summarize freshnessSeconds=max(value) by bin(timestamp, 5m) | project freshnessSeconds'
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'freshnessSeconds'
          operator: 'GreaterThan'
          threshold: 900
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: actionGroups
    }
  }
}
