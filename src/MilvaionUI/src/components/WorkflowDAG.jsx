import { useMemo } from 'react'
import Icon from './Icon'
import './WorkflowDAG.css'

const stepStatusIcons = {
  0: 'hourglass_empty',  // Pending
  1: 'sync',             // Running
  2: 'check_circle',     // Completed
  3: 'error',            // Failed
  4: 'skip_next',        // Skipped
  5: 'cancel',           // Cancelled
  6: 'schedule',         // Delayed
}

// SVG paths for icons (Material Design)
const iconPaths = {
  hourglass_empty: 'M6 2v6h.01L6 8.01 10 12l-4 4 .01.01H6V22h12v-5.99h-.01L18 16l-4-4 4-3.99-.01-.01H18V2H6zm10 14.5V20H8v-3.5l4-4 4 4zm-4-5l-4-4V4h8v3.5l-4 4z',
  sync: 'M12 4V1L8 5l4 4V6c3.31 0 6 2.69 6 6 0 1.01-.25 1.97-.7 2.8l1.46 1.46C19.54 15.03 20 13.57 20 12c0-4.42-3.58-8-8-8zm0 14c-3.31 0-6-2.69-6-6 0-1.01.25-1.97.7-2.8L5.24 7.74C4.46 8.97 4 10.43 4 12c0 4.42 3.58 8 8 8v3l4-4-4-4v3z',
  check_circle: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z',
  error: 'M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z',
  skip_next: 'M6 18l8.5-6L6 6v12zM16 6v12h2V6h-2z',
  cancel: 'M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm5 13.59L15.59 17 12 13.41 8.41 17 7 15.59 10.59 12 7 8.41 8.41 7 12 10.59 15.59 7 17 8.41 13.41 12 17 15.59z',
  schedule: 'M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z',
}

const stepStatusColors = {
  0: '#94a3b8',  // Pending - gray
  1: '#3b82f6',  // Running - blue
  2: '#22c55e',  // Completed - green
  3: '#ef4444',  // Failed - red
  4: '#a855f7',  // Skipped - purple
  5: '#6b7280',  // Cancelled - dark gray
  6: '#f59e0b',  // Delayed - amber
}

const stepStatusLabels = {
  0: 'Pending',
  1: 'Running',
  2: 'Completed',
  3: 'Failed',
  4: 'Skipped',
  5: 'Cancelled',
  6: 'Delayed',
}

/**
 * DAG visualization component for workflow steps.
 * Renders steps as nodes and edges as SVG arrows.
 */
/* eslint-disable react/prop-types */
function WorkflowDAG({ steps = [], edges = [], stepRuns = null, onStepClick = null }) {
  // Build layout using topological sort with levels
  const { nodes, edgeList, width, height } = useMemo(() => {
    if (steps.length === 0) return { nodes: [], edgeList: [], width: 400, height: 200 }

    // Build adjacency from edges
    const stepMap = new Map(steps.map(s => [s.id, s]))
    const inDeps = new Map()
    const outEdges = new Map()

    steps.forEach(s => {
      inDeps.set(s.id, [])
      outEdges.set(s.id, [])
    })

    if (edges && Array.isArray(edges)) {
      edges.forEach(e => {
        if (stepMap.has(e.sourceStepId) && stepMap.has(e.targetStepId)) {
          inDeps.get(e.targetStepId).push(e.sourceStepId)
          outEdges.get(e.sourceStepId).push(e.targetStepId)
        }
      })
    }

    // Topological sort to determine levels
    const levels = new Map()
    const queue = []
    const inDegree = new Map()

    steps.forEach(s => {
      inDegree.set(s.id, inDeps.get(s.id).length)
      if (inDeps.get(s.id).length === 0) {
        queue.push(s.id)
        levels.set(s.id, 0)
      }
    })

    let i = 0
    while (i < queue.length) {
      const nodeId = queue[i++]
      const level = levels.get(nodeId)
      for (const childId of (outEdges.get(nodeId) || [])) {
        const newDegree = inDegree.get(childId) - 1
        inDegree.set(childId, newDegree)
        const childLevel = Math.max(levels.get(childId) || 0, level + 1)
        levels.set(childId, childLevel)
        if (newDegree === 0) queue.push(childId)
      }
    }

    // Group by level
    const levelGroups = new Map()
    steps.forEach(s => {
      const level = levels.get(s.id) || 0
      if (!levelGroups.has(level)) levelGroups.set(level, [])
      levelGroups.get(level).push(s)
    })

    const nodeWidth = 200
    const nodeHeight = 70
    const hGap = 80
    const vGap = 40
    const paddingX = 40
    const paddingY = 40

    const maxLevel = Math.max(...levelGroups.keys(), 0)
    const maxNodesInLevel = Math.max(...[...levelGroups.values()].map(g => g.length), 1)

    const totalWidth = (maxLevel + 1) * (nodeWidth + hGap) + paddingX * 2
    const totalHeight = maxNodesInLevel * (nodeHeight + vGap) + paddingY * 2

    // Calculate positions
    const nodePositions = new Map()

    for (const [level, group] of levelGroups) {
      const x = paddingX + level * (nodeWidth + hGap)
      const groupHeight = group.length * (nodeHeight + vGap) - vGap
      const startY = paddingY + (totalHeight - paddingY * 2 - groupHeight) / 2

      group.forEach((step, idx) => {
        const y = startY + idx * (nodeHeight + vGap)
        // Use stored positions if available, otherwise use calculated
        const posX = step.positionX ?? x
        const posY = step.positionY ?? y
        nodePositions.set(step.id, { x: posX, y: posY })
      })
    }

    const positionedNodes = [...nodePositions.values()]
    const minX = positionedNodes.length ? Math.min(...positionedNodes.map(pos => pos.x)) : 0
    const minY = positionedNodes.length ? Math.min(...positionedNodes.map(pos => pos.y)) : 0
    const offsetX = paddingX - minX
    const offsetY = paddingY - minY

    nodePositions.forEach((pos, id) => {
      nodePositions.set(id, {
        x: pos.x + offsetX,
        y: pos.y + offsetY,
      })
    })

    const normalizedNodes = [...nodePositions.values()]
    const normalizedMaxX = normalizedNodes.length ? Math.max(...normalizedNodes.map(pos => pos.x + nodeWidth)) : totalWidth
    const normalizedMaxY = normalizedNodes.length ? Math.max(...normalizedNodes.map(pos => pos.y + nodeHeight)) : totalHeight

    // Build step run status map
    const stepRunMap = new Map()
    if (stepRuns) {
      stepRuns.forEach(sr => stepRunMap.set(sr.workflowStepId, sr))
    }

    // Create nodes
    const nodes = steps.map(step => {
      const pos = nodePositions.get(step.id) || { x: 0, y: 0 }
      const run = stepRunMap.get(step.id)
      return {
        id: step.id,
        label: step.stepName,
        jobName: step.jobDisplayName || '',
        x: pos.x,
        y: pos.y,
        width: nodeWidth,
        height: nodeHeight,
        status: run?.status ?? null,
        condition: step.condition,
        delay: step.delaySeconds,
        occurrenceId: run?.occurrenceId,
      }
    })

    // Create edges from edge definitions
    const edgeList = (edges || []).map(e => {
      if (nodePositions.has(e.sourceStepId) && nodePositions.has(e.targetStepId)) {
        const from = nodePositions.get(e.sourceStepId)
        const to = nodePositions.get(e.targetStepId)
        return {
          fromId: e.sourceStepId,
          toId: e.targetStepId,
          label: e.label,
          x1: from.x + nodeWidth,
          y1: from.y + nodeHeight / 2,
          x2: to.x,
          y2: to.y + nodeHeight / 2,
        }
      }
      return null
    }).filter(Boolean)

    return {
      nodes,
      edgeList,
      width: Math.max(totalWidth, normalizedMaxX + paddingX),
      height: Math.max(totalHeight, normalizedMaxY + paddingY),
    }
  }, [steps, edges, stepRuns])

  if (steps.length === 0) {
    return (
      <div className="dag-empty">
        <Icon name="schema" size={40} />
        <p>No steps defined</p>
      </div>
    )
  }

  return (
    <div className="dag-viewport">
      <svg width={width} height={height} className="dag-svg">
        <defs>
          <marker id="arrowhead" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="var(--text-muted)" />
          </marker>
          <marker id="arrowhead-active" markerWidth="10" markerHeight="7" refX="10" refY="3.5" orient="auto">
            <polygon points="0 0, 10 3.5, 0 7" fill="#3b82f6" />
          </marker>
        </defs>

        {/* Edges */}
        {edgeList.map((edge, i) => {
          const midX = (edge.x1 + edge.x2) / 2
          return (
            <g key={`edge-${i}`}>
              <path
                d={`M ${edge.x1} ${edge.y1} C ${midX} ${edge.y1}, ${midX} ${edge.y2}, ${edge.x2} ${edge.y2}`}
                fill="none"
                stroke="var(--border-color)"
                strokeWidth="2"
                markerEnd="url(#arrowhead)"
                className="dag-edge"
              />
              {edge.label && (
                <text
                  x={midX}
                  y={(edge.y1 + edge.y2) / 2 - 5}
                  textAnchor="middle"
                  fill="var(--text-muted)"
                  fontSize="10"
                  className="edge-label"
                >
                  {edge.label}
                </text>
              )}
            </g>
          )
        })}

        {/* Nodes */}
        {nodes.map(node => {
          const statusColor = node.status !== null ? stepStatusColors[node.status] : '#94a3b8'
          const statusLabel = node.status !== null ? stepStatusLabels[node.status] : null
          const statusIcon = node.status !== null ? stepStatusIcons[node.status] : null
          const isRunning = node.status === 1

          return (
            <g
              key={node.id}
              className="dag-node"
              transform={`translate(${node.x}, ${node.y})`}
              onClick={() => onStepClick?.(node)}
              style={{ cursor: onStepClick ? 'pointer' : 'default' }}
            >
              {/* Shadow for depth */}
              <rect
                width={node.width}
                height={node.height}
                rx="10"
                ry="10"
                fill="black"
                opacity="0.1"
                x="2"
                y="2"
              />

              {/* Main node background */}
              <rect
                width={node.width}
                height={node.height}
                rx="10"
                ry="10"
                fill="var(--bg-card, var(--bg-secondary))"
                stroke={statusColor}
                strokeWidth={isRunning ? 3 : 2}
                className={isRunning ? 'dag-node-running' : ''}
              />

              {/* Gradient overlay for running state */}
              {isRunning && (
                <>
                  <defs>
                    <linearGradient id={`gradient-${node.id}`} x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stopColor={statusColor} stopOpacity="0.1" />
                      <stop offset="100%" stopColor={statusColor} stopOpacity="0.05" />
                    </linearGradient>
                  </defs>
                  <rect
                    width={node.width}
                    height={node.height}
                    rx="10"
                    ry="10"
                    fill={`url(#gradient-${node.id})`}
                  />
                </>
              )}

              {/* Status icon at left center */}
              {statusIcon && (
                <g transform={`translate(20, ${node.height / 2})`}>
                  <title>{statusLabel || 'No status'}</title>
                  <circle cx="0" cy="0" r="14" fill={statusColor} opacity="0.15" />
                  <svg
                    x="-10"
                    y="-10"
                    width="20"
                    height="20"
                    viewBox="0 0 24 24"
                    className={isRunning ? 'dag-status-icon-spinning' : 'dag-status-icon'}
                  >
                    <path d={iconPaths[statusIcon]} fill={statusColor} />
                  </svg>
                </g>
              )}

              {/* Step name */}
              <text
                x={48}
                y={node.height / 2 - 6}
                textAnchor="start"
                fill="var(--text-primary)"
                fontSize="14"
                fontWeight="600"
              >
                {node.label.length > 18 ? node.label.substring(0, 16) + '...' : node.label}
              </text>

              {/* Job name */}
              <text
                x={48}
                y={node.height / 2 + 10}
                textAnchor="start"
                fill="var(--text-muted)"
                fontSize="11"
              >
                {node.jobName.length > 20 ? node.jobName.substring(0, 18) + '...' : node.jobName}
              </text>
            </g>
          )
        })}
      </svg>
    </div>
  )
}

export default WorkflowDAG
