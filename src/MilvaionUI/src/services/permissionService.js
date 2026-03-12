import api from './api'

export const permissionService = {
  getAll: async (params = {}) => {
    const requestBody = {
      pageNumber: 1,
      rowCount: 100000,
      sorting: {
        sortBy: 'PermissionGroup',
        type: 0
      },
      ...params
    }
    return api.patch('/permissions', requestBody)
  }
}

export default permissionService
