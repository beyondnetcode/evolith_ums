import React, { useState } from 'react';
import { useAuthStore } from '@app/stores/auth.store';
import { useI18n } from '@app/i18n/use-i18n';
import { M3Drawer } from '@shared/components/M3Drawer';
import { M3Button } from '@shared/components/M3Button';
import { Spinner } from '@shared/components/Spinner';
import { KeyValueRow } from '@shared/components/KeyValueRow';
import { CodeBadge } from '@shared/components/CodeBadge';
import { StatusBadge } from '@shared/components/StatusBadge';
import {
  User,
  Building2,
  ShieldCheck,
  Clock,
  Key,
  Globe,
  ChevronDown,
  ChevronRight,
  Copy,
  Check,
  LogOut,
  FileText,
  Flag,
  Layers,
  Database,
  Cpu,
  Activity,
} from 'lucide-react';

interface ConnectedUserDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  onLogout: () => void;
}

interface DrawerSectionProps {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
  defaultExpanded?: boolean;
}

function DrawerSection({ title, icon, children, defaultExpanded = true }: DrawerSectionProps) {
  const [isExpanded, setIsExpanded] = useState(defaultExpanded);

  return (
    <div className="mb-4">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className="w-full flex items-center justify-between px-3 py-2 rounded-xl hover:bg-m3-surface-container/50 transition-colors text-left"
      >
        <div className="flex items-center gap-2">
          <span className="text-m3-primary">{icon}</span>
          <span className="text-xs font-bold text-m3-on-surface">{title}</span>
        </div>
        {isExpanded ? (
          <ChevronDown className="w-4 h-4 text-m3-secondary" />
        ) : (
          <ChevronRight className="w-4 h-4 text-m3-secondary" />
        )}
      </button>
      {isExpanded && <div className="mt-1 px-2">{children}</div>}
    </div>
  );
}

function CopyButton({ value, label = 'Copy' }: { value: string; label?: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <button
      onClick={handleCopy}
      className="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-medium bg-m3-surface-container/50 hover:bg-m3-primary/10 text-m3-secondary hover:text-m3-primary transition-colors"
      title={`Copy ${label}`}
    >
      {copied ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3" />}
      {copied ? 'Copied' : label}
    </button>
  );
}

function UserInitialsAvatar({
  username,
  size = 'large',
}: {
  username: string;
  size?: 'large' | 'small';
}) {
  const initials = (username || '??').substring(0, 2).toUpperCase();
  const sizeClasses = size === 'large' ? 'w-16 h-16 text-xl' : 'w-8 h-8 text-xs';

  return (
    <div
      className={`${sizeClasses} rounded-full bg-gradient-to-tr from-m3-primary to-indigo-600 flex items-center justify-center font-black text-white shadow-lg`}
    >
      {initials}
    </div>
  );
}

export function ConnectedUserDrawer({ isOpen, onClose, onLogout }: ConnectedUserDrawerProps) {
  const t = useI18n();
  const user = useAuthStore(state => state.user);
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);

  if (!user || !isAuthenticated) {
    return null;
  }

  const handleLogout = () => {
    onLogout();
    onClose();
  };

  const sessionStart = localStorage.getItem('ums_session_start');
  const sessionStartDate = sessionStart ? new Date(parseInt(sessionStart, 10)) : null;

  return (
    <M3Drawer
      open={isOpen}
      onClose={onClose}
      position="right"
      title={t.profileStats || 'Connected User'}
      width="max-w-md"
    >
      <div className="p-4 space-y-4">
        {/* User Header */}
        <div className="flex items-center gap-4 p-4 bg-m3-surface-container/30 rounded-2xl border border-m3-outline/25">
          <UserInitialsAvatar username={user.username} />
          <div className="flex-1 min-w-0">
            <h3 className="text-base font-extrabold text-m3-on-surface truncate">
              {user.username}
            </h3>
            <p className="text-[11px] text-m3-secondary truncate">{user.email}</p>
            <div className="flex items-center gap-2 mt-1">
              <CodeBadge code={(user.role || 'N/A').toUpperCase()} />
              {user.tenantCode && (
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-m3-secondary-container/30 text-m3-on-secondary-container font-medium">
                  {user.tenantCode}
                </span>
              )}
            </div>
          </div>
        </div>

        {/* User Section */}
        <DrawerSection title={t.user || 'User'} icon={<User className="w-4 h-4" />}>
          <div className="space-y-2 text-xs">
            <div className="p-3 bg-m3-surface-container/20 rounded-xl space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Username</span>
                <span className="font-medium text-m3-on-surface">{user.username}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Email</span>
                <span className="font-medium text-m3-on-surface text-[10px] truncate max-w-[200px]">
                  {user.email}
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">User ID</span>
                <div className="flex items-center gap-1">
                  <span className="font-mono text-m3-primary text-[10px]">
                    {user.id ? `${user.id.substring(0, 8)}...` : 'N/A'}
                  </span>
                  {user.id && <CopyButton value={user.id} label="ID" />}
                </div>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Status</span>
                <StatusBadge status="active" label="Active" />
              </div>
            </div>
          </div>
        </DrawerSection>

        {/* Tenant Section */}
        <DrawerSection title={t.tenant || 'Tenant'} icon={<Building2 className="w-4 h-4" />}>
          <div className="space-y-2 text-xs">
            <div className="p-3 bg-m3-surface-container/20 rounded-xl space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Tenant Name</span>
                <span className="font-medium text-m3-on-surface">{user.tenantName || 'N/A'}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Tenant Code</span>
                <CodeBadge code={user.tenantCode || 'N/A'} />
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Tenant ID</span>
                <div className="flex items-center gap-1">
                  <span className="font-mono text-m3-primary text-[10px]">
                    {user.tenantId ? `${user.tenantId.substring(0, 8)}...` : 'N/A'}
                  </span>
                  {user.tenantId && <CopyButton value={user.tenantId} label="ID" />}
                </div>
              </div>
            </div>
          </div>
        </DrawerSection>

        {/* Profile Section */}
        <DrawerSection title={t.profile || 'Profile'} icon={<ShieldCheck className="w-4 h-4" />}>
          <div className="space-y-2 text-xs">
            <div className="p-3 bg-m3-surface-container/20 rounded-xl space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Profile ID</span>
                <div className="flex items-center gap-1">
                  <span className="font-mono text-m3-primary text-[10px]">
                    {user.profileId ? `${user.profileId.substring(0, 8)}...` : 'N/A'}
                  </span>
                  {user.profileId && <CopyButton value={user.profileId} label="ID" />}
                </div>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Role</span>
                <CodeBadge code={user.role || 'N/A'} />
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Permissions</span>
                <span className="font-medium text-m3-on-surface">
                  {user.permissions?.length || 0} granted
                </span>
              </div>
            </div>
          </div>
        </DrawerSection>

        {/* Session Section */}
        <DrawerSection title={t.session || 'Session'} icon={<Clock className="w-4 h-4" />}>
          <div className="space-y-2 text-xs">
            <div className="p-3 bg-m3-surface-container/20 rounded-xl space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Session Tracking ID</span>
                <div className="flex items-center gap-1">
                  <span className="font-mono text-m3-primary text-[10px]">
                    {user.sessionTrackingId
                      ? `${user.sessionTrackingId.substring(0, 8)}...`
                      : 'N/A'}
                  </span>
                  {user.sessionTrackingId && (
                    <CopyButton value={user.sessionTrackingId} label="Session" />
                  )}
                </div>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Language</span>
                <span className="font-extrabold uppercase text-m3-on-surface">EN</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-m3-secondary">Environment</span>
                <CodeBadge code={import.meta.env.PROD ? 'PROD' : 'DEV'} />
              </div>
              {sessionStartDate && (
                <div className="flex items-center justify-between">
                  <span className="text-m3-secondary">Login Time</span>
                  <span className="font-medium text-m3-on-surface">
                    {sessionStartDate.toLocaleTimeString()}
                  </span>
                </div>
              )}
            </div>
          </div>
        </DrawerSection>

        {/* Access Summary */}
        <DrawerSection
          title="Access Summary"
          icon={<FileText className="w-4 h-4" />}
          defaultExpanded={false}
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs">
            <div className="p-3 bg-m3-surface-container/20 rounded-xl flex items-center gap-2">
              <Layers className="w-4 h-4 text-m3-primary" />
              <div>
                <span className="text-[10px] text-m3-secondary">Modules</span>
                <p className="font-bold text-m3-on-surface">8</p>
              </div>
            </div>
            <div className="p-3 bg-m3-surface-container/20 rounded-xl flex items-center gap-2">
              <Database className="w-4 h-4 text-m3-primary" />
              <div>
                <span className="text-[10px] text-m3-secondary">Resources</span>
                <p className="font-bold text-m3-on-surface">24</p>
              </div>
            </div>
            <div className="p-3 bg-m3-surface-container/20 rounded-xl flex items-center gap-2">
              <Cpu className="w-4 h-4 text-m3-primary" />
              <div>
                <span className="text-[10px] text-m3-secondary">Actions</span>
                <p className="font-bold text-m3-on-surface">156</p>
              </div>
            </div>
            <div className="p-3 bg-m3-surface-container/20 rounded-xl flex items-center gap-2">
              <Flag className="w-4 h-4 text-m3-primary" />
              <div>
                <span className="text-[10px] text-m3-secondary">Flags</span>
                <p className="font-bold text-m3-on-surface">3</p>
              </div>
            </div>
          </div>
        </DrawerSection>

        {/* Technical Details */}
        <DrawerSection
          title="Technical Details"
          icon={<Activity className="w-4 h-4" />}
          defaultExpanded={false}
        >
          <div className="p-3 bg-m3-surface-container/20 rounded-xl space-y-3 text-xs">
            <div>
              <p className="text-[10px] font-bold text-m3-secondary uppercase mb-1">User Context</p>
              <pre className="text-[9px] font-mono text-m3-on-surface/70 bg-m3-surface-container/50 p-2 rounded overflow-x-auto">
                {JSON.stringify(
                  {
                    userId: user.id,
                    username: user.username,
                    email: user.email,
                    role: user.role || null,
                    tenantId: user.tenantId,
                    tenantCode: user.tenantCode,
                    profileId: user.profileId,
                    sessionTrackingId: user.sessionTrackingId,
                  },
                  null,
                  2
                )}
              </pre>
            </div>
            <div className="flex justify-end">
              <CopyButton value={JSON.stringify(user, null, 2)} label="Copy JSON" />
            </div>
          </div>
        </DrawerSection>

        {/* Logout Button */}
        <div className="pt-4 border-t border-m3-outline/25">
          <M3Button
            variant="outlined"
            onClick={handleLogout}
            className="w-full text-m3-error border-m3-error/30 hover:bg-m3-error/10"
          >
            <LogOut className="w-4 h-4 mr-2" />
            {t.logoutBtn || 'Logout'}
          </M3Button>
        </div>
      </div>
    </M3Drawer>
  );
}
