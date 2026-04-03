import api from './api'

export const activityLogService = {
  getAll: async (params = {}) => {
    const requestBody = {
      pageNumber: params.pageNumber || 1,
      rowCount: params.rowCount || 20,
      sorting: {
        sortBy: params.sortBy || 'ActivityDate',
        type: params.sortType || 1
      },
      ...params
    }
    return api.patch('/activitylogs', requestBody)
  }
}

export default activityLogService
