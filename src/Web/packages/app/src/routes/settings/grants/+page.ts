import { redirect } from "@sveltejs/kit";
import type { PageLoad } from "./$types";

// Redirect grants to sharing page (Followers & Sharing)
export const load: PageLoad = async () => {
  throw redirect(308, "/settings/sharing");
};
