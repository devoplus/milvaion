import Icon from '../../../components/Icon'

function StepConfigPanel({ step, jobs, allSteps, onChange, onClose }) {
  if (!step) return null

  const update = (field, value) => onChange({ ...step, [field]: value })

  const toggleDep = (depTempId) => {
    const current = step.dependsOnTempIds
      ? step.dependsOnTempIds.split(',').map(d => d.trim()).filter(Boolean)
      : []
    const idx = current.indexOf(depTempId)
    if (idx >= 0) current.splice(idx, 1)
    else current.push(depTempId)
    update('dependsOnTempIds', current.join(','))
  }

  const currentDeps = step.dependsOnTempIds
    ? step.dependsOnTempIds.split(',').map(d => d.trim()).filter(Boolean)
    : []

  const availableParents = allSteps.filter(s => s.tempId !== step.tempId)

  const addMapping = () =>
    update('dataMappings', [...(step.dataMappings || []), { sourceStepTempId: '', sourcePath: '', targetPath: '' }])

  const updateMapping = (i, field, value) => {
    const updated = (step.dataMappings || []).map((m, mi) => (mi === i ? { ...m, [field]: value } : m))
    update('dataMappings', updated)
  }

  const removeMapping = (i) =>
    update('dataMappings', (step.dataMappings || []).filter((_, mi) => mi !== i))

  return (
    <div className="wfb-config-panel">
      <div className="wfb-config-header">
        <h3><Icon name="tune" size={18} /> Step Config</h3>
        <button className="wfb-config-close" onClick={onClose} title="Close">
          <Icon name="chevron_right" size={20} />
        </button>
      </div>

      <div className="wfb-config-body">
        {/* Step Name */}
        <div className="wfb-field">
          <label>Step Name</label>
          <input
            className="wfb-input"
            value={step.stepName}
            onChange={e => update('stepName', e.target.value)}
            placeholder="e.g. Extract Data"
          />
        </div>

        {/* Job */}
        <div className="wfb-field">
          <label>Job</label>
          <select className="wfb-select" value={step.jobId} onChange={e => update('jobId', e.target.value)}>
            <option value="">— Select Job —</option>
            {jobs.map(j => (
              <option key={j.id} value={j.id}>{j.displayName || j.jobNameInWorker}</option>
            ))}
          </select>
        </div>

        {/* Delay */}
        <div className="wfb-field">
          <label>Delay (seconds)</label>
          <input
            className="wfb-input"
            type="number"
            min={0}
            value={step.delaySeconds}
            onChange={e => update('delaySeconds', Number(e.target.value) || 0)}
          />
        </div>

        {/* Condition */}
        <div className="wfb-field">
          <label>Condition</label>
          <input
            className="wfb-input"
            value={step.condition}
            onChange={e => update('condition', e.target.value)}
            placeholder="@status == 'Completed'"
          />
          <span className="wfb-hint">Leave empty to always execute</span>
        </div>

        {/* Dependencies */}
        {availableParents.length > 0 && (
          <div className="wfb-field">
            <label>Depends On</label>
            <div className="wfb-deps-list">
              {availableParents.map(p => (
                <label key={p.tempId} className="wfb-dep-item">
                  <input
                    type="checkbox"
                    checked={currentDeps.includes(p.tempId)}
                    onChange={() => toggleDep(p.tempId)}
                  />
                  <span>{p.stepName || 'Unnamed Step'}</span>
                </label>
              ))}
            </div>
            <span className="wfb-hint">Or draw arrows on the canvas</span>
          </div>
        )}

        {/* Job Data Override */}
        <div className="wfb-field">
          <label>Job Data Override</label>
          <textarea
            className="wfb-textarea"
            rows={4}
            value={step.jobDataOverride}
            onChange={e => update('jobDataOverride', e.target.value)}
            placeholder={'{"key": "value"}'}
          />
        </div>

        {/* Data Mappings */}
        <div className="wfb-field">
          <div className="wfb-field-header">
            <label>Data Mappings</label>
            <button className="wfb-btn-xs" onClick={addMapping}>
              <Icon name="add" size={14} /> Add
            </button>
          </div>
          {(step.dataMappings || []).length === 0 && (
            <span className="wfb-hint">No mappings defined</span>
          )}
          {(step.dataMappings || []).map((m, i) => (
            <div key={i} className="wfb-mapping-row">
              <select
                className="wfb-input-sm"
                value={m.sourceStepTempId}
                onChange={e => updateMapping(i, 'sourceStepTempId', e.target.value)}
              >
                <option value="">Step</option>
                {availableParents.map(p => (
                  <option key={p.tempId} value={p.tempId}>{p.stepName || p.tempId}</option>
                ))}
              </select>
              <input
                className="wfb-input-sm"
                value={m.sourcePath}
                onChange={e => updateMapping(i, 'sourcePath', e.target.value)}
                placeholder="$.field"
              />
              <Icon name="arrow_forward" size={14} />
              <input
                className="wfb-input-sm"
                value={m.targetPath}
                onChange={e => updateMapping(i, 'targetPath', e.target.value)}
                placeholder="$.target"
              />
              <button className="wfb-btn-delete-sm" onClick={() => removeMapping(i)}>
                <Icon name="delete" size={14} />
              </button>
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

export default StepConfigPanel
