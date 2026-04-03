import api from './api'

const metricReportService = {
  async getReports(metricType = null, rowCount = 20) {
    const requestBody = {
      pageNumber: 1,
      rowCount,
      metricType: metricType,
      sorting: {
        sortBy: "GeneratedAt",
        type: 1
      }
    }
    return api.patch('/metricreports', requestBody)
  },

  async getReportById(id) {
    return api.get('/metricreports', { params: { Id: id } })
  },

  async getLatestReportByType(metricType) {
    return api.get('/metricreports/latest', { params: { MetricType: metricType } })
  },

  async deleteReport(id) {
    return api.delete('/metricreports', { params: { Id: id } })
  },

  async deleteOldReports(olderThanDays) {
    return api.delete('/metricreports/cleanup', {
      params: { OlderThanDays: olderThanDays }
    })
  }
}

export default metricReportService
