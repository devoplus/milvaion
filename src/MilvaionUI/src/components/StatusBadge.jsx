import PropTypes from 'prop-types'
import './StatusBadge.css'

const STATUS_CONFIG = {
  Success: { label: '✅ Success', className: 'success' },
  Failed: { label: '❌ Failed', className: 'failed' },
  Running: { label: '🔄 Running', className: 'running' },
  Pending: { label: '⏳ Pending', className: 'pending' },
  Active: { label: '✅ Active', className: 'active' },
  Inactive: { label: '⏸️ Inactive', className: 'inactive' },
}

function StatusBadge({ status, compact = false }) {
  const config = STATUS_CONFIG[status] || { label: status, className: 'default' }

  return (
    <span className={`status-badge ${config.className} ${compact ? 'compact' : ''}`}>
      {compact ? config.label.split(' ')[0] : config.label}
    </span>
  )
}

StatusBadge.propTypes = {
  status: PropTypes.string.isRequired,
  compact: PropTypes.bool
}

export default StatusBadge
