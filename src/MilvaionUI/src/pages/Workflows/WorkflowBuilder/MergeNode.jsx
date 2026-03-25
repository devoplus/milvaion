import { memo } from 'react'
import { Handle, Position } from 'reactflow'
import Icon from '../../../components/Icon'

/* eslint-disable react/prop-types */
function MergeNode({ data, selected }) {
  const { step, onDelete } = data

  return (
    <div className={`wfb-merge-node${selected ? ' wfb-merge-node--selected' : ''}`}>
      {/* Input handles - top */}
      <Handle
        type="target"
        position={Position.Top}
        id="input-1"
        style={{ left: '30%' }}
      />
      <Handle
        type="target"
        position={Position.Top}
        id="input-2"
        style={{ left: '70%' }}
      />

      <div className="wfb-merge-node-header">
        <span className="wfb-merge-node-title" title={step.stepName}>
          {step.stepName || <em>Merge</em>}
        </span>
        <button
          className="wfb-step-node-delete"
          onClick={e => { e.stopPropagation(); onDelete(step.tempId) }}
          title="Delete node"
        >
          <Icon name="close" size={13} />
        </button>
      </div>

      <div className="wfb-merge-node-body">
        <Icon name="call_merge" size={18} />
      </div>

      {/* Output handle - bottom */}
      <Handle
        type="source"
        position={Position.Bottom}
        id="output"
      />
    </div>
  )
}

export default memo(MergeNode)
