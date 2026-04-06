// Scroll-spy and scroll utilities extracted from ProfileComponent.

export interface SectionDef {
  id: string;
  icon: string;
  labelKey: string;
}

export const PROFILE_SECTIONS: SectionDef[] = [
  { id: 'personal', icon: 'pi pi-user', labelKey: 'pages.profile.personal_info' },
  { id: 'security', icon: 'pi pi-lock', labelKey: 'pages.profile.security_title' },
  { id: 'addresses', icon: 'pi pi-map-marker', labelKey: 'pages.profile.addresses_title' },
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
