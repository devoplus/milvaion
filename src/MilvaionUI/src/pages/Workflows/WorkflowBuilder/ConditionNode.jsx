import { memo } from 'react'
import { Handle, Position } from 'reactflow'
import Icon from '../../../components/Icon'

/* eslint-disable react/prop-types */
function ConditionNode({ data, selected }) {
  const { step, onDelete } = data

  let expression = ''
  try {
    const config = JSON.parse(step.nodeConfigJson || '{}')
    expression = config.expression || ''
  } catch {
    // Ignore parse errors
  }

  return (
    <div className={`wfb-condition-node${selected ? ' wfb-condition-node--selected' : ''}`}>
      {/* Input handle - top */}
      <Handle
        type="target"
        position={Position.Top}
        id="input"
      />

      <div className="wfb-condition-node-header">
        <span className="wfb-condition-node-title" title={step.stepName}>
          {step.stepName || <em>Condition</em>}
        </span>
        <button
          className="wfb-step-node-delete"
          onClick={e => { e.stopPropagation(); onDelete(step.tempId) }}
          title="Delete node"
        >
          <Icon name="close" size={13} />
        </button>
      </div>

      <div className="wfb-condition-node-body">
        <Icon name="alt_route" size={18} />
        {expression && (
          <span className="wfb-condition-expr" title={expression}>
            {expression}
          </span>
        )}
      </div>

      {/* True handle - right */}
      <Handle
        type="source"
        position={Position.Right}
        id="true"
        style={{ top: '40%', background: '#22c55e' }}
      />
      <span className="wfb-port-label wfb-port-label--true">TRUE</span>

      {/* False handle - bottom */}
      <Handle
        type="source"
        position={Position.Bottom}
        id="false"
        style={{ background: '#ef4444' }}
      />
      <span className="wfb-port-label wfb-port-label--false">FALSE</span>
    </div>
  )
}

export default memo(ConditionNode)
