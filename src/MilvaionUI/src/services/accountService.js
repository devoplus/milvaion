import api from './api'

export const accountService = {
  getDetail: async (userId) => {
    return api.get('/account/detail', { params: { userId } })
  },

  changePassword: async (userName, oldPassword, newPassword) => {
    return api.put('/account/password/change', {
      userName,
      oldPassword,
      newPassword
    })
  }
}

export default accountService
