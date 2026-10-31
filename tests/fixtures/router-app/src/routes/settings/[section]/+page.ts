import { redirect } from '@sveltejs/kit';
import { sections } from '$lib/util';

export function load({ params }) {
  if (!sections.includes(params.section)) redirect(307, '/settings/appearance');
  return { section: params.section };
}
