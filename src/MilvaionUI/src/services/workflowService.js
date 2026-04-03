import api from './api'

export const workflowService = {
  // Get all workflows
  getAll: async (params = {}) => {
    const requestBody = {
      pageNumber: 1,
      rowCount: 100000,
      ...params,
      sorting: {
        sortBy: "Id",
        type: 1
      }
    }
    return api.patch('/workflows', requestBody)
  },

  // Get workflow by ID
  getById: async (workflowId) => {
    return api.get('/workflows/workflow', { params: { workflowId } })
  },

  // Create new workflow
  create: async (workflowData) => {
    return api.post('/workflows/workflow', workflowData)
  },

  // Update workflow
  update: async (workflowId, workflowData) => {
    return api.put('/workflows/workflow', { workflowId, ...workflowData })
  },

  // Delete workflow
  delete: async (workflowId) => {
    return api.delete('/workflows/workflow', {
      params: { WorkflowId: workflowId }
    })
  },

  // Trigger workflow run
  trigger: async (workflowId, reason = 'Manual trigger') => {
    return api.post('/workflows/workflow/trigger', { workflowId, reason })
  },

  // Cancel workflow run
  cancelRun: async (workflowRunId, reason = 'Manual cancellation') => {
    return api.post('/workflows/workflow/cancel', { workflowRunId, reason })
  },

  // Get workflow runs
  getRuns: async (workflowId = null, params = {}) => {
    const requestBody = {
      pageNumber: params.pageNumber || 1,
      rowCount: params.rowCount || 20,
      workflowId,
      sorting: {
        sortBy: "Id",
        type: 1
      }
    }
    return api.patch('/workflows/runs', requestBody)
  },

  // Get workflow run detail (with step states for DAG visualization)
  getRunDetail: async (runId) => {
    return api.get('/workflows/runs/run', { params: { runId } })
  },
}

export default workflowService
