import { memo } from 'react'
import { Handle, Position } from 'reactflow'
import Icon from '../../../components/Icon'

/* eslint-disable react/prop-types */
function StepNode({ data, selected }) {
  const { step, jobsMap, onDelete } = data
  const job = jobsMap?.[step.jobId]

  return (
    <div className={`wfb-step-node${selected ? ' wfb-step-node--selected' : ''}${!step.jobId ? ' wfb-step-node--invalid' : ''}`}>
      {/* Target handle - sol taraf */}
      <Handle
        type="target"
        position={Position.Left}
      />

      <div className="wfb-step-node-header">
        <span className="wfb-step-node-title" title={step.stepName}>
          {step.stepName || <em>Unnamed Step</em>}
        </span>
        <button
          className="wfb-step-node-delete"
          onClick={e => { e.stopPropagation(); onDelete(step.tempId) }}
          title="Delete step"
        >
          <Icon name="close" size={13} />
        </button>
      </div>

      <div className="wfb-step-node-body">
        <Icon name="work" size={13} />
        <span className="wfb-step-node-job" title={job?.displayName}>
          {job?.displayName || <em className="wfb-step-node-no-job">No job selected</em>}
        </span>
      </div>

      {step.delaySeconds > 0 && (
        <div className="wfb-step-node-meta">
          <span className="wfb-step-meta-tag">
            <Icon name="schedule" size={11} /> {step.delaySeconds}s
          </span>
        </div>
      )}

      {/* Source handle - sağ taraf */}
      <Handle
        type="source"
        position={Position.Right}
      />
    </div>
  )
}

export default memo(StepNode)
