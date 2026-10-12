/**
 * Registro de iconos de navegación.
 *
 * El servidor publica en cada nodo del grafo un **identificador** de icono (`"building"`,
 * `"layout-dashboard"`…), nunca un SVG ni una URL: si guardara el recurso, la base de datos
 * quedaría atada a la biblioteca gráfica de un frontend concreto y cambiarla exigiría migrar datos.
 *
 * Este módulo es el otro extremo de esa convención: traduce el identificador al icono real. Es el
 * único punto del cliente que conoce la biblioteca; añadir un icono nuevo al catálogo del servidor
 * solo obliga a añadir una entrada aquí.
 */
import React from 'react';
import {
  Activity,
  Bell,
  Building2,
  CalendarDays,
  CalendarRange,
  Cpu,
  FileText,
  Flag,
  FolderKanban,
  GitMerge,
  History,
  Inbox,
  LayoutDashboard,
  LayoutGrid,
  ListChecks,
  Package,
  Receipt,
  SatelliteDish,
  ScrollText,
  Settings,
  ShieldCheck,
  Truck,
  User,
  UserRound,
  Users,
} from 'lucide-react';

type ComponenteIcono = React.ComponentType<{ className?: string }>;

const REGISTRO: Record<string, ComponenteIcono> = {
  activity: Activity,
  bell: Bell,
  building: Building2,
  'calendar-days': CalendarDays,
  'calendar-range': CalendarRange,
  cpu: Cpu,
  'file-text': FileText,
  flag: Flag,
  'folder-kanban': FolderKanban,
  'git-merge': GitMerge,
  history: History,
  inbox: Inbox,
  'layout-dashboard': LayoutDashboard,
  'layout-grid': LayoutGrid,
  'list-checks': ListChecks,
  package: Package,
  receipt: Receipt,
  'satellite-dish': SatelliteDish,
  'scroll-text': ScrollText,
  settings: Settings,
  'shield-check': ShieldCheck,
  truck: Truck,
  user: User,
  'user-round': UserRound,
  users: Users,
};

/** Icono de respaldo: un identificador desconocido no debe dejar la fila sin icono y desalineada. */
const RESPALDO: ComponenteIcono = LayoutGrid;

export const iconoConocido = (identificador: string | null | undefined): boolean =>
  identificador != null && identificador in REGISTRO;

/** Resuelve el identificador publicado por el grafo al icono que pinta el shell. */
export const iconoDeNodo = (
  identificador: string | null | undefined,
  className = 'w-4 h-4'
): React.ReactNode => {
  const Icono = (identificador ? REGISTRO[identificador] : undefined) ?? RESPALDO;
  return <Icono className={className} />;
};
