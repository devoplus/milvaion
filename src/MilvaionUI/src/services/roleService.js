import api from './api'

const wrapForUpdate = (value, isUpdated = true) => ({
  value: value ?? null,
  isUpdated
})

export const roleService = {
  getAll: async (params = {}) => {
    const requestBody = {
      pageNumber: params.pageNumber || 1,
      rowCount: params.rowCount || 20,
      sorting: {
        sortBy: params.sortBy || 'Id',
        type: params.sortType || 1
      },
      ...params
    }
    return api.patch('/roles', requestBody)
  },

  getById: async (roleId) => {
    return api.get('/roles/role', { params: { roleId } })
  },

  create: async (roleData) => {
    return api.post('/roles/role', roleData)
  },

  update: async (id, roleData, updatedFields = null) => {
    const fieldsToUpdate = updatedFields || Object.keys(roleData)

    const requestBody = {
      id,
      name: wrapForUpdate(roleData.name, fieldsToUpdate.includes('name')),
      permissionIdList: wrapForUpdate(roleData.permissionIdList, fieldsToUpdate.includes('permissionIdList'))
    }

    return api.put('/roles/role', requestBody)
  },

  delete: async (roleId) => {
    return api.delete('/roles/role', { params: { roleId } })
  }
}

export default roleService
