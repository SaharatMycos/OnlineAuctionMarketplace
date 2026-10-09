import { apiOrNull } from "@/lib/api";
import SignInForm from "./SignInForm";

export const dynamic = "force-dynamic";

/** Dev sign-in (plan: dev auth now, a real identity provider later). */
export default async function SignInPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  const users = (await apiOrNull<{ email: string; displayName: string }[]>("/v1/dev/users")) ?? [];

  return (
    <div className="narrow">
      <h1>Sign in</h1>
      <p className="muted">
        Development sign-in: pick a demo account or type any email. A real identity provider replaces this before launch.
      </p>
      <SignInForm users={users} next={next ?? "/"} />
    </div>
  );
}
