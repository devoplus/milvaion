import { useState } from 'react'
import Icon from '../../../components/Icon'
import DataMappingEditor from '../DataMappingEditor'

/* eslint-disable react/prop-types */

function SchemaSection({ label, icon, fields, color }) {
  const [open, setOpen] = useState(false)
  if (!fields?.length) return null
  return (
    <div className="wfb-schema-section" style={{ '--schema-color': color }}>
      <button type="button" className="wfb-schema-toggle" onClick={() => setOpen(p => !p)}>
        <Icon name={icon} size={13} />
        <span>{label}</span>
        <span className="wfb-schema-count">{fields.length} field{fields.length !== 1 ? 's' : ''}</span>
        <Icon name={open ? 'expand_less' : 'expand_more'} size={14} className="wfb-schema-chevron" />
      </button>
      {open && (
        <div className="wfb-schema-fields">
          {fields.map(f => (
            <div key={f.name} className="wfb-schema-field" style={f.depth > 0 ? { paddingLeft: `${f.depth * 12 + 8}px` } : undefined}>
              <span className="wfb-schema-field-name">$.{f.name}</span>
              <span className={`wfb-schema-field-type wfb-schema-type--${f.type}`}>{f.format || f.type}</span>
              {f.description && <span className="wfb-schema-field-desc" title={f.description}>{f.description}</span>}
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

function StepConfigPanel({ step, jobs, allSteps, schemasMap = {}, onChange, onClose }) {
  if (!step) return null

  const update = (field, value) => onChange({ ...step, [field]: value })

  const selectedJob = step.jobId ? jobs.find(j => j.id === step.jobId) : null
  const jobSchema = selectedJob ? (schemasMap[selectedJob.jobType] || { dataFields: [], resultFields: [] }) : null

  return (
    <div className="wfb-config-panel">
      <div className="wfb-config-header">
        <h3><Icon name="tune" size={18} /> Step Config</h3>
        <button className="wfb-config-close" onClick={onClose} title="Close">
          <Icon name="chevron_right" size={20} />
        </button>
      </div>

      <div className="wfb-config-body">
        {/* Node Type */}
        <div className="wfb-field">
          <label>Node Type</label>
          <select className="wfb-select" value={step.nodeType ?? 0} onChange={e => update('nodeType', Number(e.target.value))}>
            <option value={0}>Task</option>
            <option value={1}>Condition</option>
            <option value={2}>Merge</option>
          </select>
        </div>

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

        {/* Job (only for Task nodes) */}
        {(step.nodeType === 0 || step.nodeType === undefined) && (
          <div className="wfb-field">
            <label>Job</label>
            <select className="wfb-select" value={step.jobId} onChange={e => update('jobId', e.target.value)}>
              <option value="">— Select Job —</option>
              {jobs.map(j => (
                <option key={j.id} value={j.id}>{j.displayName || j.jobNameInWorker}</option>
              ))}
            </select>
          </div>
        )}

        {/* Job Schema Preview */}
        {jobSchema && (jobSchema.dataFields?.length > 0 || jobSchema.resultFields?.length > 0) && (
          <div className="wfb-schema-preview">
            <SchemaSection
              label="Input Schema"
              icon="input"
              fields={jobSchema.dataFields}
              color="var(--accent-color)"
            />
            <SchemaSection
              label="Output Schema"
              icon="output"
              fields={jobSchema.resultFields}
              color="#10b981"
            />
          </div>
        )}

        {/* Delay (only for Task nodes — virtual nodes don't support delay yet) */}
        {(step.nodeType === 0 || step.nodeType === undefined) && (
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
        )}

        {/* Node Config (for Condition nodes) */}
        {step.nodeType === 1 && (
          <div className="wfb-field">
            <label>Condition Expression</label>
            <input
              className="wfb-input"
              value={step.nodeConfigJson ? JSON.parse(step.nodeConfigJson || '{}').expression || '' : ''}
              onChange={e => {
                try {
                  const config = { expression: e.target.value }
                  update('nodeConfigJson', JSON.stringify(config))
                } catch {
                  // Ignore
                }
              }}
              placeholder="@status == 'Completed' || $.count > 0"
            />
            <span className="wfb-hint">Evaluated on parent node results</span>
          </div>
        )}

        {/* Job Data Override (only for Task nodes) */}
        {(step.nodeType === 0 || step.nodeType === undefined) && (
          <div className="wfb-field">
            <div className="wfb-field-header">
              <label>Job Data Override</label>
              {step.dataMappings?.length > 0 && (
                <span className="wfb-badge wfb-badge--warn">
                  <Icon name="swap_horiz" size={11} /> Overridden by mappings
                </span>
              )}
            </div>
            <textarea
              className="wfb-textarea"
              rows={4}
              value={step.dataMappings?.length > 0 ? '' : (step.jobDataOverride || '')}
              disabled={step.dataMappings?.length > 0}
              onChange={e => update('jobDataOverride', e.target.value)}
              placeholder={step.dataMappings?.length > 0 ? 'Disabled — data mappings are active' : '{"key": "value"}'}
            />
          </div>
        )}

        {/* Data Mappings (only for Task nodes) */}
        {(step.nodeType === 0 || step.nodeType === undefined) && (
          <div className="wfb-field">
            <DataMappingEditor
              mappings={step.dataMappings || []}
              onChange={(newMappings) => {
                const hadMappings = (step.dataMappings?.length || 0) > 0
                const hasMappings = newMappings.length > 0
                update('dataMappings', newMappings)
                if (!hadMappings && hasMappings && step.jobDataOverride)
                  onChange({ ...step, dataMappings: newMappings, jobDataOverride: '' })
              }}
              steps={allSteps}
              currentStepTempId={step.tempId}
              jobs={jobs}
              currentStepJobId={step.jobId}
              schemasMap={schemasMap}
            />
          </div>
        )}
      </div>
    </div>
  )
}

export default StepConfigPanel
