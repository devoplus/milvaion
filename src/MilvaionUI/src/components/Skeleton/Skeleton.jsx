import './Skeleton.css'

/**
 * Reusable skeleton loading component
 * @param {string} variant - 'text' | 'circular' | 'rectangular' | 'card' | 'table-row' | 'stat-card'
 * @param {number} width - Custom width (optional)
 * @param {number} height - Custom height (optional)
 * @param {number} count - Number of skeleton items to render
 * @param {string} className - Additional CSS classes
 */
function Skeleton({ 
  variant = 'text', 
  width, 
  height, 
  count = 1, 
  className = '',
  animation = 'pulse' // 'pulse' | 'wave' | 'none'
}) {
  const style = {
    ...(width && { width: typeof width === 'number' ? `${width}px` : width }),
    ...(height && { height: typeof height === 'number' ? `${height}px` : height }),
  }

  const items = Array.from({ length: count }, (_, i) => (
    <div 
      key={i}
      className={`skeleton skeleton-${variant} skeleton-${animation} ${className}`}
      style={style}
      aria-hidden="true"
    />
  ))

  return count === 1 ? items[0] : <>{items}</>
}

// Pre-built skeleton patterns for common UI elements
export function SkeletonStatCard() {
  return (
    <div className="skeleton-stat-card">
      <div className="skeleton-stat-icon">
        <Skeleton variant="circular" width={32} height={32} />
      </div>
      <div className="skeleton-stat-content">
        <Skeleton variant="text" width={60} height={24} />
        <Skeleton variant="text" width={80} height={14} />
      </div>
    </div>
  )
}

export function SkeletonTableRow({ columns = 5 }) {
  return (
    <tr className="skeleton-table-row">
      {Array.from({ length: columns }, (_, i) => (
        <td key={i}>
          <Skeleton variant="text" width={i === 0 ? '80%' : '60%'} height={16} />
        </td>
      ))}
    </tr>
  )
}

export function SkeletonTable({ rows = 5, columns = 5 }) {
  return (
    <div className="skeleton-table-container">
      <table className="skeleton-table">
        <thead>
          <tr>
            {Array.from({ length: columns }, (_, i) => (
              <th key={i}>
                <Skeleton variant="text" width="70%" height={14} />
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {Array.from({ length: rows }, (_, i) => (
            <SkeletonTableRow key={i} columns={columns} />
          ))}
        </tbody>
      </table>
    </div>
  )
}

export function SkeletonCard({ lines = 3 }) {
  return (
    <div className="skeleton-card">
      <div className="skeleton-card-header">
        <Skeleton variant="text" width="40%" height={20} />
      </div>
      <div className="skeleton-card-body">
        {Array.from({ length: lines }, (_, i) => (
          <Skeleton 
            key={i} 
            variant="text" 
            width={i === lines - 1 ? '60%' : '100%'} 
            height={14} 
          />
        ))}
      </div>
    </div>
  )
}

export function SkeletonDashboard() {
  return (
    <div className="skeleton-dashboard">
      {/* Quick Stats */}
      <div className="skeleton-quick-stats">
        <SkeletonStatCard />
        <SkeletonStatCard />
        <SkeletonStatCard />
        <SkeletonStatCard />
      </div>
      
      {/* Main Grid */}
      <div className="skeleton-dashboard-grid">
        <SkeletonCard lines={4} />
        <SkeletonCard lines={4} />
        <SkeletonCard lines={6} />
      </div>
    </div>
  )
}

export function SkeletonJobList({ rows = 10 }) {
  return (
    <div className="skeleton-job-list">
      {/* Search/Filter Bar */}
      <div className="skeleton-toolbar">
        <Skeleton variant="rectangular" width={200} height={36} />
        <Skeleton variant="rectangular" width={120} height={36} />
      </div>
      
      {/* Table */}
      <SkeletonTable rows={rows} columns={6} />
    </div>
  )
}

export function SkeletonDetail() {
  return (
    <div className="skeleton-detail">
      <div className="skeleton-detail-header">
        <Skeleton variant="text" width="30%" height={28} />
        <Skeleton variant="text" width="20%" height={20} />
      </div>
      <div className="skeleton-detail-body">
        <SkeletonCard lines={5} />
        <SkeletonCard lines={3} />
      </div>
    </div>
  )
}

export default Skeleton
