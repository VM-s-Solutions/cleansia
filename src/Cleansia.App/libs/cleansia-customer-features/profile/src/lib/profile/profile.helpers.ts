// Scroll-spy and scroll utilities extracted from ProfileComponent.

export interface SectionDef {
  id: string;
  icon: string;
  labelKey: string;
}

/**
 * The rail, in the approved board's order. Notifications are their OWN section
 * rather than a block inside Preferences: eleven toggles are the largest thing
 * on this page and were buried under a language picker and a theme switch.
 * `danger` is the board's "Data and privacy" — it holds the data export as
 * well as account deletion, so it is not only a danger zone.
 */
export const PROFILE_SECTIONS: SectionDef[] = [
  { id: 'personal', icon: 'pi pi-user', labelKey: 'pages.profile.personal_info' },
  { id: 'addresses', icon: 'pi pi-map-marker', labelKey: 'pages.profile.addresses_title' },
  { id: 'security', icon: 'pi pi-lock', labelKey: 'pages.profile.security_title' },
  { id: 'notifications', icon: 'pi pi-bell', labelKey: 'pages.profile.notifications.title' },
  { id: 'preferences', icon: 'pi pi-sliders-h', labelKey: 'pages.profile.preferences_title' },
  { id: 'danger', icon: 'pi pi-exclamation-triangle', labelKey: 'pages.profile.danger_zone_title' },
];

export function setupScrollSpy(
  sections: SectionDef[],
  setActiveSection: (id: string) => void
): IntersectionObserver {
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          const id = entry.target.id.replace('profile-', '');
          setActiveSection(id);
        }
      }
    },
    { rootMargin: '-20% 0px -60% 0px', threshold: 0 }
  );

  for (const section of sections) {
    const el = document.getElementById(`profile-${section.id}`);
    if (el) {
      observer.observe(el);
    }
  }

  return observer;
}
