import { useState, useEffect, useCallback, useRef } from 'react'
import { useNavigate, useParams, Link } from 'react-router-dom'
import workflowService from '../../services/workflowService'
import jobService from '../../services/jobService'
import Icon from '../../components/Icon'
import Modal from '../../components/Modal'
import { useModal } from '../../hooks/useModal'
import CronExpressionInput from '../../components/CronExpressionInput'
import './WorkflowForm.css'

const failureStrategies = [
  { value: 0, label: 'Stop on First Failure' },
  { value: 1, label: 'Continue on Failure' },
  { value: 2, label: 'Retry then Stop' },
  { value: 3, label: 'Retry then Continue' },
]

function WorkflowForm() {
  const tempIdCounter = useRef(1)
  const { id } = useParams()
  const navigate = useNavigate()
  const isEditMode = !!id
  const { modalProps, showSuccess, showError } = useModal()

  const [jobs, setJobs] = useState([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [tagInput, setTagInput] = useState('')

  const [form, setForm] = useState({
    name: '',
    description: '',
    tags: [],
    isActive: true,
    failureStrategy: 0,
    maxStepRetries: 0,
    timeoutSeconds: '',
    cronExpression: '',
  })

  const [steps, setSteps] = useState([])

  // Load available jobs
  const loadJobs = useCallback(async () => {
    try {
      const response = await jobService.getAll()
      setJobs(response?.data || [])
    } catch {
      // ignore
    }
  }, [])

  // Load workflow data for edit mode
  const loadWorkflow = useCallback(async () => {
    try {
      setLoading(true)
      const response = await workflowService.getById(id)
      const data = response.data

      setForm({
        name: data.name || '',
        description: data.description || '',
        tags: data.tags ? data.tags.split(',').map(t => t.trim()).filter(t => t) : [],
        isActive: data.isActive,
        failureStrategy: data.failureStrategy ?? 0,
        maxStepRetries: data.maxStepRetries || 0,
        timeoutSeconds: data.timeoutSeconds || '',
        cronExpression: data.cronExpression || '',
      })

      // Load steps
      if (data.steps && data.steps.length > 0) {
        setSteps(data.steps.map(s => ({
          tempId: s.id ? s.id.toString() : `step-${tempIdCounter.current++}`,
          jobId: s.jobId || '',
          stepName: s.stepName || '',
          order: s.order || 0,
          dependsOnTempIds: s.dependsOnStepIds || '',
          condition: s.condition || '',
          delaySeconds: s.delaySeconds || 0,
          jobDataOverride: s.jobDataOverride || '',
          dataMappings: s.dataMappings ? deserializeMappings(s.dataMappings) : [],
        })))
      }
    } catch (err) {
      showError('Failed to load workflow')
      console.error(err)
    } finally {
      setLoading(false)
    }
  }, [id, showError])

  useEffect(() => {
    const initializeForm = async () => {
      await loadJobs()
      if (isEditMode) {
        await loadWorkflow()
      }
    }
    initializeForm()
  }, [isEditMode, loadJobs, loadWorkflow])

  const handleFormChange = (field, value) => {
    setForm(prev => ({ ...prev, [field]: value }))
  }

  const handleAddTag = (e) => {
    if (e.type === 'keydown' && e.key !== 'Enter') return
    e.preventDefault()
    const tag = tagInput.trim()
    if (tag && !form.tags.includes(tag)) {
      setForm(prev => ({ ...prev, tags: [...prev.tags, tag] }))
    }
    setTagInput('')
  }

  const handleRemoveTag = (tagToRemove) => {
    setForm(prev => ({ ...prev, tags: prev.tags.filter(t => t !== tagToRemove) }))
  }

  // Step management
  const addStep = () => {
    setSteps(prev => [
      ...prev,
      {
        tempId: `step-${tempIdCounter.current++}`,
        jobId: '',
        stepName: '',
        order: prev.length + 1,
        dependsOnTempIds: '',
        condition: '',
        delaySeconds: 0,
        jobDataOverride: '',
        dataMappings: [],
      }
    ])
  }

  const updateStep = (index, field, value) => {
    setSteps(prev => prev.map((s, i) => i === index ? { ...s, [field]: value } : s))
  }

  const removeStep = (index) => {
    const removedTempId = steps[index].tempId
    setSteps(prev => {
      const updated = prev.filter((_, i) => i !== index)
      // Remove references to the deleted step from dependencies
      return updated.map((s, i) => ({
        ...s,
        order: i + 1,
        dependsOnTempIds: s.dependsOnTempIds
          ? s.dependsOnTempIds.split(',').map(d => d.trim()).filter(d => d !== removedTempId).join(',')
          : ''
      }))
    })
  }

  const toggleDependency = (stepIndex, depTempId) => {
    setSteps(prev => prev.map((s, i) => {
      if (i !== stepIndex) return s
      const current = s.dependsOnTempIds ? s.dependsOnTempIds.split(',').map(d => d.trim()).filter(Boolean) : []
      const idx = current.indexOf(depTempId)
      if (idx >= 0) {
        current.splice(idx, 1)
      } else {
        current.push(depTempId)
      }
      return { ...s, dependsOnTempIds: current.join(',') }
    }))
  }

  const addMapping = (stepIndex) => {
    setSteps(prev => prev.map((s, i) => {
      if (i !== stepIndex) return s
      return { ...s, dataMappings: [...s.dataMappings, { sourceStepTempId: '', sourcePath: '', targetPath: '' }] }
    }))
  }

  const updateMapping = (stepIndex, mapIndex, field, value) => {
    setSteps(prev => prev.map((s, i) => {
      if (i !== stepIndex) return s
      const updated = s.dataMappings.map((m, mi) => mi === mapIndex ? { ...m, [field]: value } : m)
      return { ...s, dataMappings: updated }
    }))
  }

  const removeMapping = (stepIndex, mapIndex) => {
    setSteps(prev => prev.map((s, i) => {
      if (i !== stepIndex) return s
      return { ...s, dataMappings: s.dataMappings.filter((_, mi) => mi !== mapIndex) }
    }))
  }

  // Deserialize dataMappings from backend JSON format to array for editing
  const deserializeMappings = (dataMappingsJson) => {
    if (!dataMappingsJson) return []
    try {
      const obj = typeof dataMappingsJson === 'string' ? JSON.parse(dataMappingsJson) : dataMappingsJson
      return Object.entries(obj).map(([sourceKey, targetPath]) => {
        const [sourceStepTempId, sourcePath] = sourceKey.includes(':') 
          ? sourceKey.split(':', 2) 
          : ['', sourceKey]
        return { sourceStepTempId, sourcePath, targetPath }
      })
    } catch {
      return []
    }
  }

  // Serialize dataMappings array into the JSON format the backend expects:
  // { "stepTempId:sourcePath": "targetPath" } or { "sourcePath": "targetPath" }
  const serializeMappings = (mappings) => {
    if (!mappings || mappings.length === 0) return null
    const obj = {}
    for (const m of mappings) {
      if (!m.sourcePath || !m.targetPath) continue
      const sourceKey = m.sourceStepTempId ? `${m.sourceStepTempId}:${m.sourcePath}` : m.sourcePath
      obj[sourceKey] = m.targetPath
    }
    return Object.keys(obj).length > 0 ? JSON.stringify(obj) : null
  }

  const handleSubmit = async (e) => {
    e.preventDefault()

    if (!form.name.trim()) {
      showError('Workflow name is required.')
      return
    }

    if (steps.length === 0) {
      showError('Add at least one step.')
      return
    }

    for (const step of steps) {
      if (!step.jobId) {
        showError(`Step "${step.stepName || step.order}" has no job selected.`)
        return
      }
      if (!step.stepName.trim()) {
        showError(`Step #${step.order} needs a name.`)
        return
      }
    }

    try {
      setSaving(true)

      const payload = {
        name: form.name.trim(),
        description: form.description.trim(),
        tags: form.tags.join(','),
        isActive: form.isActive,
        failureStrategy: form.failureStrategy,
        maxStepRetries: form.maxStepRetries,
        timeoutSeconds: form.timeoutSeconds ? parseInt(form.timeoutSeconds) : null,
        cronExpression: form.cronExpression?.trim() || null,
        steps: steps.map(s => ({
          tempId: s.tempId,
          jobId: s.jobId,
          stepName: s.stepName,
          order: s.order,
          dependsOnTempIds: s.dependsOnTempIds || null,
          condition: s.condition || null,
          dataMappings: serializeMappings(s.dataMappings),
          delaySeconds: s.delaySeconds || 0,
          jobDataOverride: s.jobDataOverride || null,
        }))
      }

      let result
      if (isEditMode) {
        result = await workflowService.update(id, payload)
      } else {
        result = await workflowService.create(payload)
      }

      if (result?.isSuccess) {
        showSuccess(isEditMode ? 'Workflow updated successfully!' : 'Workflow created successfully!')
        setTimeout(() => navigate(`/workflows/${isEditMode ? id : result.data}`), 1000)
      } else {
        showError(result?.message || `Failed to ${isEditMode ? 'update' : 'create'} workflow.`)
      }
    } catch (err) {
      showError(err?.response?.data?.message || `Failed to ${isEditMode ? 'update' : 'create'} workflow.`)
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <div className="workflow-form-loading"><Icon name="hourglass_empty" size={24} /> Loading workflow...</div>
  }

  return (
    <div className="workflow-form-page">
      <div className="wf-form-header">
        <div className="wf-form-header-left">
          <Link to={isEditMode ? `/workflows/${id}` : "/workflows"} className="wf-back-btn" title="Back">
            <Icon name="arrow_back" size={24} />
          </Link>
          <div className="wf-form-header-content">
            <h1>{isEditMode ? 'Edit Workflow' : 'Create Workflow'}</h1>
            <p className="wf-form-subtitle">{isEditMode ? 'Modify workflow configuration and steps' : 'Define a workflow by chaining jobs into a DAG pipeline'}</p>
          </div>
        </div>
        <div className="wf-form-actions">
          <Link to={isEditMode ? `/workflows/${id}` : "/workflows"} className="wf-cancel-btn">Cancel</Link>
          <button type="submit" form="wf-form" className="wf-submit-btn" disabled={saving}>
            {saving ? (
              <><Icon name="hourglass_empty" size={18} /> {isEditMode ? 'Updating...' : 'Creating...'}</>
            ) : (
              <><Icon name="check" size={18} /> {isEditMode ? 'Update Workflow' : 'Create Workflow'}</>
            )}
          </button>
        </div>
      </div>

      <form id="wf-form" onSubmit={handleSubmit} className="workflow-form">
        <div className="wf-top-row">
          {/* General Info */}
          <div className="form-section">
            <h2><Icon name="info" size={20} /> General</h2>
            <div className="wf-general-fields">
              <div className="form-group">
                <label htmlFor="wf-name">Workflow Name *</label>
                <input
                  id="wf-name"
                  type="text"
                  value={form.name}
                  onChange={e => handleFormChange('name', e.target.value)}
                  placeholder="e.g. Daily Report Pipeline"
                  required
                />
              </div>
              <div className="form-group">
                <label htmlFor="wf-desc">Description</label>
                <textarea
                  id="wf-desc"
                  value={form.description}
                  onChange={e => handleFormChange('description', e.target.value)}
                  placeholder="Describe what this workflow does..."
                  rows={3}
                />
              </div>
              <div className="form-group">
                <label htmlFor="wf-tags">Tags</label>
                <div className="wf-tags-input-container">
                  <div className="wf-tag-input-wrapper">
                    <input
                      type="text"
                      id="wf-tags"
                      value={tagInput}
                      onChange={e => setTagInput(e.target.value)}
                      onKeyDown={handleAddTag}
                      placeholder="Type and press Enter to add tag"
                    />
                    <button type="button" onClick={handleAddTag} className="wf-tag-add-btn">Add</button>
                  </div>
                  {form.tags.length > 0 && (
                    <div className="wf-tags-list">
                      {form.tags.map((tag, i) => (
                        <span key={i} className="wf-tag">
                          {tag}
                          <button type="button" onClick={() => handleRemoveTag(tag)} className="wf-tag-remove" title="Remove tag">×</button>
                        </span>
                      ))}
                    </div>
                  )}
                </div>
              </div>
              <div className="form-group">
                <label className="wf-switch-label">
                  <span className="wf-switch-label-text">Active Status</span>
                  <div className="wf-switch-container">
                    <input
                      type="checkbox"
                      checked={form.isActive}
                      onChange={e => handleFormChange('isActive', e.target.checked)}
                      id="wf-isActive"
                      className="wf-switch-input"
                    />
                    <label htmlFor="wf-isActive" className="wf-switch">
                      <span className="wf-switch-slider"></span>
                    </label>
                    <span className="wf-switch-status">
                      {form.isActive ? 'Active' : 'Inactive'}
                    </span>
                  </div>
                </label>
              </div>
            </div>
          </div>

          {/* Settings */}
          <div className="form-section">
            <h2><Icon name="tune" size={20} /> Settings</h2>
            <div className="wf-settings-fields">
              <div className="form-group">
                <label htmlFor="wf-strategy">Failure Strategy</label>
                <select
                  id="wf-strategy"
                  value={form.failureStrategy}
                  onChange={e => handleFormChange('failureStrategy', parseInt(e.target.value))}
                >
                  {failureStrategies.map(s => (
                    <option key={s.value} value={s.value}>{s.label}</option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label htmlFor="wf-retries">Max Step Retries</label>
                <input
                  id="wf-retries"
                  type="number"
                  min="0"
                  value={form.maxStepRetries}
                  onChange={e => handleFormChange('maxStepRetries', parseInt(e.target.value) || 0)}
                />
              </div>
              <div className="form-group">
                <label htmlFor="wf-timeout">Timeout (seconds)</label>
                <input
                  id="wf-timeout"
                  type="number"
                  min="0"
                  value={form.timeoutSeconds}
                  onChange={e => handleFormChange('timeoutSeconds', e.target.value)}
                  placeholder="No timeout"
                />
              </div>
              <div className="form-group wf-cron-field">
                <label htmlFor="wf-cron">Schedule (Cron Expression)</label>
                <CronExpressionInput
                  value={form.cronExpression}
                  onChange={(e) => handleFormChange('cronExpression', e.target.value)}
                />
                <small className="wf-form-hint">Leave empty for manual-only trigger</small>
              </div>

            </div>
          </div>
        </div>

        {/* Steps */}
        <div className="form-section">
          <div className="section-header">
            <h2><Icon name="layers" size={20} /> Steps ({steps.length})</h2>
            <button type="button" className="wf-add-step-btn" onClick={addStep}>
              <Icon name="add" size={16} /> Add Step
            </button>
          </div>

          {steps.length === 0 ? (
            <div className="empty-steps">
              <Icon name="layers" size={40} />
              <p>No steps yet. Click "Add Step" to build your workflow.</p>
            </div>
          ) : (
            <div className="steps-list">
              {steps.map((step, index) => (
                <div key={step.tempId} className="step-card">
                  <div className="step-card-header">
                    <span className="step-number">#{step.order}</span>
                    <span className="step-temp-id">{step.tempId}</span>
                    <button
                      type="button"
                      className="wf-remove-step-btn"
                      onClick={() => removeStep(index)}
                      title="Remove step"
                    >
                      <Icon name="close" size={18} />
                    </button>
                  </div>

                  <div className="step-form-grid">
                    <div className="form-group">
                      <label>Step Name *</label>
                      <input
                        type="text"
                        value={step.stepName}
                        onChange={e => updateStep(index, 'stepName', e.target.value)}
                        placeholder="e.g. Fetch Data"
                      />
                    </div>
                    <div className="form-group">
                      <label>Job *</label>
                      <select
                        value={step.jobId}
                        onChange={e => updateStep(index, 'jobId', e.target.value)}
                      >
                        <option value="">Select a job...</option>
                        {jobs.map(j => (
                          <option key={j.id} value={j.id}>{j.displayName || j.jobNameInWorker}</option>
                        ))}
                      </select>
                    </div>
                    <div className="form-group">
                      <label>Delay (seconds)</label>
                      <input
                        type="number"
                        min="0"
                        value={step.delaySeconds}
                        onChange={e => updateStep(index, 'delaySeconds', parseInt(e.target.value) || 0)}
                      />
                    </div>
                    <div className="form-group">
                      <label>Condition</label>
                      <input
                        type="text"
                        value={step.condition}
                        onChange={e => updateStep(index, 'condition', e.target.value)}
                        placeholder="e.g. $.status == 'approved'"
                      />
                    </div>
                    <div className="form-group form-group-full">
                      <label>Job Data Override (JSON)</label>
                      <textarea
                        value={step.jobDataOverride}
                        onChange={e => updateStep(index, 'jobDataOverride', e.target.value)}
                        placeholder='{"key": "value"}'
                        rows={2}
                      />
                    </div>
                  </div>

                  {/* Data Mappings — pass previous step output to this step's job data */}
                  {index > 0 && (
                    <div className="step-data-mappings">
                      <div className="mapping-header">
                        <label><Icon name="swap_horiz" size={16} /> Data Mappings</label>
                        <button type="button" className="wf-mapping-add-btn" onClick={() => addMapping(index)}>
                          <Icon name="add" size={14} /> Add Mapping
                        </button>
                      </div>
                      {step.dataMappings.length === 0 ? (
                        <p className="mapping-hint">Map output fields from previous steps into this step's job data.</p>
                      ) : (
                        <div className="mapping-rows">
                          {step.dataMappings.map((mapping, mi) => {
                            const parentSteps = steps.filter((_, pi) => pi !== index)
                            const selectedJob = mapping.sourceStepTempId
                              ? steps.find(s => s.tempId === mapping.sourceStepTempId)
                              : null
                            const selectedJobInfo = selectedJob ? jobs.find(j => j.id === selectedJob.jobId) : null
                            const currentJobInfo = step.jobId ? jobs.find(j => j.id === step.jobId) : null

                            // Try to extract field names from job data JSON for hints
                            const getFieldHints = (jobInfo) => {
                              if (!jobInfo?.jobData) return []
                              try {
                                return Object.keys(JSON.parse(jobInfo.jobData))
                              } catch { return [] }
                            }

                            const sourceHints = getFieldHints(selectedJobInfo)
                            const targetHints = getFieldHints(currentJobInfo)

                            return (
                              <div key={mi} className="mapping-row">
                                <div className="mapping-source">
                                  <select
                                    value={mapping.sourceStepTempId}
                                    onChange={e => updateMapping(index, mi, 'sourceStepTempId', e.target.value)}
                                    title="Source step"
                                  >
                                    <option value="">Any parent</option>
                                    {parentSteps.map(ps => (
                                      <option key={ps.tempId} value={ps.tempId}>{ps.stepName || `#${ps.order}`}</option>
                                    ))}
                                  </select>
                                  <div className="mapping-path-input">
                                    <span className="path-prefix">$.</span>
                                    <input
                                      type="text"
                                      value={mapping.sourcePath}
                                      onChange={e => updateMapping(index, mi, 'sourcePath', e.target.value)}
                                      placeholder={sourceHints.length > 0 ? `e.g. ${sourceHints[0]}` : 'field or * for all'}
                                      list={`src-hints-${step.tempId}-${mi}`}
                                    />
                                    {sourceHints.length > 0 && (
                                      <datalist id={`src-hints-${step.tempId}-${mi}`}>
                                        <option value="*">Entire result</option>
                                        {sourceHints.map(h => <option key={h} value={h} />)}
                                      </datalist>
                                    )}
                                  </div>
                                </div>
                                <Icon name="arrow_forward" size={16} className="mapping-arrow" />
                                <div className="mapping-target">
                                  <div className="mapping-path-input">
                                    <span className="path-prefix">$.</span>
                                    <input
                                      type="text"
                                      value={mapping.targetPath}
                                      onChange={e => updateMapping(index, mi, 'targetPath', e.target.value)}
                                      placeholder={targetHints.length > 0 ? `e.g. ${targetHints[0]}` : 'target field'}
                                      list={`tgt-hints-${step.tempId}-${mi}`}
                                    />
                                    {targetHints.length > 0 && (
                                      <datalist id={`tgt-hints-${step.tempId}-${mi}`}>
                                        {targetHints.map(h => <option key={h} value={h} />)}
                                      </datalist>
                                    )}
                                  </div>
                                </div>
                                <button
                                  type="button"
                                  className="wf-remove-step-btn"
                                  onClick={() => removeMapping(index, mi)}
                                  title="Remove mapping"
                                >
                                  <Icon name="close" size={16} />
                                </button>
                              </div>
                            )
                          })}
                        </div>
                      )}
                    </div>
                  )}

                  {/* Dependencies */}
                  {index > 0 && (
                    <div className="step-dependencies">
                      <label>Depends on:</label>
                      <div className="dep-chips">
                        {steps.filter((_, i) => i !== index).map(other => {
                          const isSelected = step.dependsOnTempIds?.split(',').map(d => d.trim()).includes(other.tempId)
                          return (
                            <button
                              key={other.tempId}
                              type="button"
                              className={`dep-chip ${isSelected ? 'selected' : ''}`}
                              onClick={() => toggleDependency(index, other.tempId)}
                            >
                              {other.stepName || `#${other.order}`}
                              {isSelected && <Icon name="check" size={14} />}
                            </button>
                          )
                        })}
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>

        </form>

      <Modal {...modalProps} />
    </div>
  )
}

export default WorkflowForm
