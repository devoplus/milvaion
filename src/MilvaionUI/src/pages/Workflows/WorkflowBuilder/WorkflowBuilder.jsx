import { useState, useEffect, useCallback, useMemo } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  applyNodeChanges,
  applyEdgeChanges,
  MarkerType,
  Panel,
  useReactFlow,
  ReactFlowProvider,
} from 'reactflow'
import 'reactflow/dist/style.css'
import workflowService from '../../../services/workflowService'
import jobService from '../../../services/jobService'
import Icon from '../../../components/Icon'
import Modal from '../../../components/Modal'
import { useModal } from '../../../hooks/useModal'
import CronExpressionInput from '../../../components/CronExpressionInput'
import StepNode from './StepNode'
import StepConfigPanel from './StepConfigPanel'
import './WorkflowBuilder.css'

const nodeTypes = { stepNode: StepNode }

const failureStrategies = [
  { value: 0, label: 'Stop on First Failure' },
  { value: 1, label: 'Continue on Failure' },
]

const defaultEdgeOptions = {
  markerEnd: { type: MarkerType.ArrowClosed, color: '#646cff' },
  style: { stroke: '#646cff', strokeWidth: 2 },
  animated: true,
}

let _tempIdSeq = 1
const newTempId = () => `step-${_tempIdSeq++}`

function stepsToNodes(steps, jobsMap, onDelete) {
  return steps.map((s, idx) => ({
    id: s.tempId,
    type: 'stepNode',
    position: { x: s.positionX ?? idx * 240 + 40, y: s.positionY ?? 100 },
    data: { step: s, jobsMap, onDelete },
    connectable: true,
  }))
}

function stepsToEdges(steps) {
  const edges = []
  for (const s of steps) {
    if (!s.dependsOnTempIds) continue
    for (const depId of s.dependsOnTempIds.split(',').map(d => d.trim()).filter(Boolean)) {
      edges.push({
        id: `e-${depId}-${s.tempId}`,
        source: depId,
        target: s.tempId,
        ...defaultEdgeOptions,
      })
    }
  }
  return edges
}

function serializeMappings(mappings) {
  if (!mappings?.length) return null
  const obj = {}
  mappings.forEach(m => {
    if (m.sourceStepTempId && m.sourcePath && m.targetPath)
      obj[`${m.sourceStepTempId}:${m.sourcePath}`] = m.targetPath
  })
  return Object.keys(obj).length ? JSON.stringify(obj) : null
}

function deserializeMappings(json) {
  if (!json) return []
  try {
    const obj = typeof json === 'string' ? JSON.parse(json) : json
    return Object.entries(obj).map(([key, targetPath]) => {
      const [sourceStepTempId, sourcePath] = key.includes(':') ? key.split(':', 2) : ['', key]
      return { sourceStepTempId, sourcePath, targetPath }
    })
  } catch { return [] }
}

// ─── Inner component (needs useReactFlow hook) ───────────────────────────────
function WorkflowBuilderInner() {
  const { id } = useParams()
  const navigate = useNavigate()
  const isEdit = !!id
  const { modalProps, showSuccess, showError } = useModal()
  const { fitView } = useReactFlow()

  const [jobs, setJobs] = useState([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [selectedStepId, setSelectedStepId] = useState(null)
  const [showSettings, setShowSettings] = useState(false)
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
  const [nodes, setNodes] = useState([])
  const [edges, setEdges] = useState([])

  const onNodesChange = useCallback((changes) => {
    setNodes(prev => applyNodeChanges(changes, prev))
  }, [])

  const onEdgesChange = useCallback((changes) => {
    setEdges(prev => applyEdgeChanges(changes, prev))
  }, [])

  const jobsMap = useMemo(() => Object.fromEntries(jobs.map(j => [j.id, j])), [jobs])

  // ── Delete step ─────────────────────────────────────────────────────────────
  const handleDeleteStep = useCallback((tempId) => {
    setSteps(prev =>
      prev.filter(s => s.tempId !== tempId).map(s => ({
        ...s,
        dependsOnTempIds: s.dependsOnTempIds
          ? s.dependsOnTempIds.split(',').map(d => d.trim()).filter(d => d !== tempId).join(',')
          : '',
      }))
    )
    setSelectedStepId(prev => prev === tempId ? null : prev)
  }, [])

  // ── Sync steps → nodes + edges ────────────────────────────────────────────────
  useEffect(() => {
    const newNodes = stepsToNodes(steps, jobsMap, handleDeleteStep)
    const newEdges = stepsToEdges(steps)
    setNodes(newNodes)
    setEdges(newEdges)
  }, [steps, jobsMap, handleDeleteStep])

  // ── Load jobs ────────────────────────────────────────────────────────────────
  useEffect(() => {
    jobService.getAll().then(r => setJobs(r?.data || [])).catch(() => {})
  }, [])

  // ── Load workflow (edit mode) ─────────────────────────────────────────────────
  useEffect(() => {
    if (!isEdit) return
    setLoading(true)
    workflowService.getById(id)
      .then(r => {
        const d = r.data
        setForm({
          name: d.name || '',
          description: d.description || '',
          tags: d.tags ? d.tags.split(',').map(t => t.trim()).filter(Boolean) : [],
          isActive: d.isActive,
          failureStrategy: d.failureStrategy ?? 0,
          maxStepRetries: d.maxStepRetries || 0,
          timeoutSeconds: d.timeoutSeconds || '',
          cronExpression: d.cronExpression || '',
        })
        if (d.steps?.length > 0) {
          setSteps(d.steps.map(s => ({
            tempId: s.id?.toString() ?? newTempId(),
            jobId: s.jobId || '',
            stepName: s.stepName || '',
            order: s.order || 0,
            dependsOnTempIds: s.dependsOnStepIds || '',
            condition: s.condition || '',
            delaySeconds: s.delaySeconds || 0,
            jobDataOverride: s.jobDataOverride || '',
            dataMappings: deserializeMappings(s.dataMappings),
            positionX: s.positionX,
            positionY: s.positionY,
          })))
          setTimeout(() => fitView({ padding: 0.2, duration: 400 }), 150)
        }
      })
      .catch(() => showError('Failed to load workflow'))
      .finally(() => setLoading(false))
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, isEdit])

  // ── Add step ─────────────────────────────────────────────────────────────────
  const addStep = () => {
    const tempId = newTempId()
    const col = steps.length % 4
    const row = Math.floor(steps.length / 4)
    setSteps(prev => [...prev, {
      tempId, jobId: '', stepName: '', order: prev.length + 1,
      dependsOnTempIds: '', condition: '', delaySeconds: 0,
      jobDataOverride: '', dataMappings: [],
      positionX: 40 + col * 260,
      positionY: 80 + row * 180,
    }])
    setSelectedStepId(tempId)
  }

  // ── Drag stop → persist position ─────────────────────────────────────────────
  const onNodeDragStop = useCallback((_, node) => {
    setSteps(prev =>
      prev.map(s => s.tempId === node.id
        ? { ...s, positionX: Math.round(node.position.x), positionY: Math.round(node.position.y) }
        : s
      )
    )
  }, [])

  // ── Connect edge → add dependency ─────────────────────────────────────────────
  const onConnect = useCallback((params) => {
    if (!params.source || !params.target) return
    if (params.source === params.target) return

    // steps state'ini güncelle - useEffect edges'i otomatik oluşturacak
    setSteps(prev => {
      return prev.map(s => {
        if (s.tempId !== params.target) return s
        const cur = s.dependsOnTempIds ? s.dependsOnTempIds.split(',').filter(Boolean) : []
        if (cur.includes(params.source)) return s
        return { ...s, dependsOnTempIds: [...cur, params.source].join(',') }
      })
    })
  }, [])

  // ── Delete edge → remove dependency
  const onEdgesDelete = useCallback((deleted) => {
    setSteps(prev =>
      prev.map(s => {
        const toRemove = deleted.filter(e => e.target === s.tempId).map(e => e.source)
        if (!toRemove.length) return s
        const cur = s.dependsOnTempIds ? s.dependsOnTempIds.split(',').map(d => d.trim()).filter(Boolean) : []
        return { ...s, dependsOnTempIds: cur.filter(d => !toRemove.includes(d)).join(',') }
      })
    )
  }, [])

  const onNodeClick = useCallback((_, node) => {
    setSelectedStepId(prev => prev === node.id ? null : node.id)
  }, [])

  const onPaneClick = useCallback(() => setSelectedStepId(null), [])

  const updateSelectedStep = (updated) =>
    setSteps(prev => prev.map(s => s.tempId === updated.tempId ? { ...s, ...updated } : s))

  const selectedStep = steps.find(s => s.tempId === selectedStepId) ?? null

  // ── Tags ──────────────────────────────────────────────────────────────────────
  const handleAddTag = (e) => {
    if (e.type === 'keydown' && e.key !== 'Enter') return
    e.preventDefault()
    const tag = tagInput.trim()
    if (tag && !form.tags.includes(tag)) setForm(p => ({ ...p, tags: [...p.tags, tag] }))
    setTagInput('')
  }

  // ── Save ──────────────────────────────────────────────────────────────────────
  const handleSave = async () => {
    if (!form.name.trim()) { showError('Workflow name is required'); return }
    if (steps.length === 0) { showError('At least one step is required'); return }

    setSaving(true)
    try {
      const payload = {
        name: form.name,
        description: form.description,
        tags: form.tags.join(','),
        isActive: form.isActive,
        failureStrategy: Number(form.failureStrategy),
        maxStepRetries: Number(form.maxStepRetries) || 0,
        timeoutSeconds: form.timeoutSeconds ? Number(form.timeoutSeconds) : null,
        cronExpression: form.cronExpression || null,
        steps: steps.map((s, idx) => ({
          tempId: s.tempId,
          jobId: s.jobId,
          stepName: s.stepName,
          order: idx + 1,
          dependsOnTempIds: s.dependsOnTempIds || '',
          condition: s.condition || '',
          delaySeconds: Number(s.delaySeconds) || 0,
          jobDataOverride: s.jobDataOverride || null,
          dataMappings: serializeMappings(s.dataMappings),
          positionX: s.positionX,
          positionY: s.positionY,
        })),
      }

      if (isEdit) {
        await workflowService.update(id, payload)
        await showSuccess('Workflow saved successfully!')
      } else {
        const res = await workflowService.create(payload)
        await showSuccess('Workflow created!')
        navigate(`/workflows/${res.data}/builder`)
      }
    } catch (err) {
      showError('Failed to save workflow')
      console.error(err)
    } finally {
      setSaving(false)
    }
  }

  // ─────────────────────────────────────────────────────────────────────────────

  if (loading) {
    return (
      <div className="wfb-loading">
        <Icon name="hourglass_empty" size={28} /> Loading workflow...
      </div>
    )
  }

  return (
    <div className="wfb-page">
      {/* ── Toolbar ── */}
      <div className="wfb-toolbar">
        <div className="wfb-toolbar-left">
          <Link to={isEdit ? `/workflows/${id}` : '/workflows'} className="wfb-back-btn" title="Back">
            <Icon name="arrow_back" size={22} />
          </Link>
          <input
            className="wfb-name-input"
            value={form.name}
            onChange={e => setForm(p => ({ ...p, name: e.target.value }))}
            placeholder="Workflow name..."
          />
          <label className="wfb-active-toggle">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={e => setForm(p => ({ ...p, isActive: e.target.checked }))}
            />
            <span className={`wfb-active-label ${form.isActive ? 'is-active' : 'is-inactive'}`}>
              {form.isActive ? 'Active' : 'Inactive'}
            </span>
          </label>
        </div>

        <div className="wfb-toolbar-right">
          <span className="wfb-step-count">{steps.length} step{steps.length !== 1 ? 's' : ''}</span>
          <button
            className={`wfb-toolbar-btn${showSettings ? ' wfb-toolbar-btn--pressed' : ''}`}
            onClick={() => setShowSettings(p => !p)}
          >
            <Icon name="settings" size={17} /> Settings
          </button>
          <button className="wfb-toolbar-btn wfb-toolbar-btn--add" onClick={addStep}>
            <Icon name="add" size={17} /> Add Step
          </button>
          <button className="wfb-toolbar-btn wfb-toolbar-btn--save" onClick={handleSave} disabled={saving}>
            <Icon name={saving ? 'hourglass_empty' : 'save'} size={17} />
            {saving ? 'Saving…' : 'Save'}
          </button>
        </div>
      </div>

      {/* ── Settings Bar ── */}
      {showSettings && (
        <div className="wfb-settings-bar">
          <div className="wfb-settings-row">
            <div className="wfb-setting-field">
              <label>Description</label>
              <input
                className="wfb-input"
                value={form.description}
                onChange={e => setForm(p => ({ ...p, description: e.target.value }))}
                placeholder="Optional description..."
              />
            </div>

            <div className="wfb-setting-field">
              <label>Tags</label>
              <div className="wfb-tags-row">
                {form.tags.map(t => (
                  <span key={t} className="wfb-tag">
                    {t}
                    <button onClick={() => setForm(p => ({ ...p, tags: p.tags.filter(x => x !== t) }))}>×</button>
                  </span>
                ))}
                <input
                  className="wfb-tag-input"
                  value={tagInput}
                  onChange={e => setTagInput(e.target.value)}
                  onKeyDown={handleAddTag}
                  onBlur={handleAddTag}
                  placeholder="Add tag…"
                />
              </div>
            </div>

            <div className="wfb-setting-field wfb-setting-field--sm">
              <label>Failure Strategy</label>
              <select
                className="wfb-select"
                value={form.failureStrategy}
                onChange={e => setForm(p => ({ ...p, failureStrategy: Number(e.target.value) }))}
              >
                {failureStrategies.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </div>

            <div className="wfb-setting-field wfb-setting-field--xs">
              <label>Max Retries</label>
              <input
                className="wfb-input"
                type="number"
                min={0}
                value={form.maxStepRetries}
                onChange={e => setForm(p => ({ ...p, maxStepRetries: Number(e.target.value) || 0 }))}
              />
            </div>

            <div className="wfb-setting-field wfb-setting-field--xs">
              <label>Timeout (s)</label>
              <input
                className="wfb-input"
                type="number"
                min={0}
                value={form.timeoutSeconds}
                onChange={e => setForm(p => ({ ...p, timeoutSeconds: e.target.value }))}
                placeholder="None"
              />
            </div>
          </div>

          <div className="wfb-settings-row">
            <div className="wfb-setting-field wfb-setting-field--full">
              <label>Cron Schedule</label>
              <CronExpressionInput
                value={form.cronExpression}
                onChange={e => setForm(p => ({ ...p, cronExpression: e.target.value }))}
              />
            </div>
          </div>
        </div>
      )}

      {/* ── Canvas + Config Panel ── */}
      <div className="wfb-canvas-wrapper">
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={onNodesChange}
          onEdgesChange={onEdgesChange}
          onConnect={onConnect}
          onEdgesDelete={onEdgesDelete}
          onNodeDragStop={onNodeDragStop}
          onNodeClick={onNodeClick}
          onPaneClick={onPaneClick}
          nodeTypes={nodeTypes}
          defaultEdgeOptions={defaultEdgeOptions}
          proOptions={{ hideAttribution: true }}
          fitView
          fitViewOptions={{ padding: 0.2 }}
          deleteKeyCode="Delete"
          className="wfb-reactflow"
          style={{ width: '100%', height: '100%' }}
        >
          <Background variant="dots" gap={16} size={1} color="var(--border-color)" />
          <Controls showInteractive={false} />
          <MiniMap
            nodeStrokeWidth={3}
            pannable
            zoomable
            nodeColor="var(--accent-color)"
            maskColor="rgba(0,0,0,0.35)"
            style={{ background: 'var(--bg-secondary)' }}
          />

          {steps.length === 0 && (
            <Panel position="top-center" className="wfb-empty-hint">
              <Icon name="add_circle_outline" size={18} />
              Click <strong>Add Step</strong> to build your workflow DAG
            </Panel>
          )}
        </ReactFlow>

        {selectedStep && (
          <StepConfigPanel
            step={selectedStep}
            jobs={jobs}
            allSteps={steps}
            onChange={updateSelectedStep}
            onClose={() => setSelectedStepId(null)}
          />
        )}
      </div>

      <Modal {...modalProps} />
    </div>
  )
}

// Wrap with provider
export default function WorkflowBuilder() {
  return (
    <ReactFlowProvider>
      <WorkflowBuilderInner />
    </ReactFlowProvider>
  )
}
