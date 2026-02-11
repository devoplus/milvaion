import { useState, useEffect, useCallback, useRef } from 'react'
import Icon from './Icon'
import notificationService from '../services/notificationService'
import './NotificationPanel.css'

function NotificationPanel({ isOpen, onClose }) {
  const [notifications, setNotifications] = useState([])
  const [loading, setLoading] = useState(false)
  const [hasMore, setHasMore] = useState(true)
  const [pageIndex, setPageIndex] = useState(0)
  const panelRef = useRef(null)
  const PAGE_SIZE = 20

  const fetchNotifications = useCallback(async (page = 0, append = false) => {
    setLoading(true)
    try {
      const response = await notificationService.getNotifications({
        pageIndex: page,
        itemCount: PAGE_SIZE,
      })

      if (response?.isSuccess) {
        const items = response.data ?? []
        setNotifications(prev => append ? [...prev, ...items] : items)
        setHasMore(items.length >= PAGE_SIZE)
        setPageIndex(page)
      }
    } catch {
      // silently ignore
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (isOpen) {
      setPageIndex(0)
      fetchNotifications(0)
    }
  }, [isOpen, fetchNotifications])

  useEffect(() => {
    const handleEscape = (e) => {
      if (e.key === 'Escape' && isOpen) onClose()
    }
    document.addEventListener('keydown', handleEscape)
    return () => document.removeEventListener('keydown', handleEscape)
  }, [isOpen, onClose])

  const handleMarkAllSeen = async () => {
    try {
      const response = await notificationService.markAsSeen({ markAll: true })
      if (response?.isSuccess) {
        setNotifications(prev =>
          prev.map(n => ({ ...n, seenDate: n.seenDate ?? new Date().toISOString() }))
        )
      }
    } catch {
      // silently ignore
    }
  }

  const handleMarkSeen = async (id) => {
    try {
      const response = await notificationService.markAsSeen({ notificationIdList: [id] })
      if (response?.isSuccess) {
        setNotifications(prev =>
          prev.map(n => n.id === id ? { ...n, seenDate: new Date().toISOString() } : n)
        )
      }
    } catch {
      // silently ignore
    }
  }

  const handleDelete = async (id) => {
    try {
      const response = await notificationService.deleteNotifications({ notificationIdList: [id] })
      if (response?.isSuccess) {
        setNotifications(prev => prev.filter(n => n.id !== id))
      }
    } catch {
      // silently ignore
    }
  }

  const handleDeleteAll = async () => {
    try {
      const response = await notificationService.deleteNotifications({ deleteAll: true })
      if (response?.isSuccess) {
        setNotifications([])
      }
    } catch {
      // silently ignore
    }
  }

  const handleLoadMore = () => {
    if (!loading && hasMore) {
      fetchNotifications(pageIndex + 1, true)
    }
  }

  const unseenCount = notifications.filter(n => !n.seenDate).length

  const formatTime = (dateStr) => {
    if (!dateStr) return ''
    const date = new Date(dateStr)
    const now = new Date()
    const diffMs = now - date
    const diffMin = Math.floor(diffMs / 60000)
    if (diffMin < 1) return 'Just now'
    if (diffMin < 60) return `${diffMin}m ago`
    const diffH = Math.floor(diffMin / 60)
    if (diffH < 24) return `${diffH}h ago`
    const diffD = Math.floor(diffH / 24)
    if (diffD < 7) return `${diffD}d ago`
    return date.toLocaleDateString()
  }

  const getTypeIcon = (type) => {
    switch (type) {
      case 0: return 'info'
      case 1: return 'warning'
      case 2: return 'error'
      case 3: return 'check_circle'
      default: return 'notifications'
    }
  }

  const getTypeClass = (type) => {
    switch (type) {
      case 0: return 'info'
      case 1: return 'warning'
      case 2: return 'error'
      case 3: return 'success'
      default: return 'info'
    }
  }

  return (
    <>
      <div
        className={`notification-backdrop ${isOpen ? 'visible' : ''}`}
        onClick={onClose}
        aria-hidden="true"
      />
      <div
        ref={panelRef}
        className={`notification-panel ${isOpen ? 'open' : ''}`}
      >
        <div className="notification-panel-header">
          <h3>Notifications</h3>
          <div className="notification-header-actions">
            {unseenCount > 0 && (
              <button
                className="notification-header-btn"
                onClick={handleMarkAllSeen}
                title="Mark all as read"
              >
                <Icon name="done_all" size={18} />
              </button>
            )}
            {notifications.length > 0 && (
              <button
                className="notification-header-btn danger"
                onClick={handleDeleteAll}
                title="Delete all"
              >
                <Icon name="delete_sweep" size={18} />
              </button>
            )}
            <button
              className="notification-header-btn"
              onClick={onClose}
              title="Close"
            >
              <Icon name="close" size={18} />
            </button>
          </div>
        </div>

        <div className="notification-panel-body">
          {loading && notifications.length === 0 ? (
            <div className="notification-empty">
              <Icon name="hourglass_empty" size={40} />
              <p>Loading...</p>
            </div>
          ) : notifications.length === 0 ? (
            <div className="notification-empty">
              <Icon name="notifications_none" size={40} />
              <p>No notifications</p>
            </div>
          ) : (
            <>
              {notifications.map(n => (
                <div
                  key={n.id}
                  className={`notification-item ${!n.seenDate ? 'unseen' : ''}`}
                >
                  <div className={`notification-icon ${getTypeClass(n.type)}`}>
                    <Icon name={getTypeIcon(n.type)} size={20} />
                  </div>
                  <div className="notification-content">
                    <p className="notification-text">
                      {n.text || n.typeDescription || 'Notification'}
                    </p>
                    <span className="notification-time">
                      {formatTime(n.creationDate)}
                    </span>
                  </div>
                  <div className="notification-actions">
                    {!n.seenDate && (
                      <button
                        className="notification-action-btn"
                        onClick={() => handleMarkSeen(n.id)}
                        title="Mark as read"
                      >
                        <Icon name="check" size={16} />
                      </button>
                    )}
                    <button
                      className="notification-action-btn danger"
                      onClick={() => handleDelete(n.id)}
                      title="Delete"
                    >
                      <Icon name="close" size={16} />
                    </button>
                  </div>
                </div>
              ))}
              {hasMore && (
                <button
                  className="notification-load-more"
                  onClick={handleLoadMore}
                  disabled={loading}
                >
                  {loading ? 'Loading...' : 'Load more'}
                </button>
              )}
            </>
          )}
        </div>
      </div>
    </>
  )
}

export default NotificationPanel
