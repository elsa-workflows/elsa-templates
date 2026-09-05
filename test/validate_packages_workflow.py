"""Source-level guards for the package publication workflow.

These checks keep the recovery path fail-closed while the workflow itself is
validated with actionlint in CI or during release preparation.
"""

from pathlib import Path
import unittest


WORKFLOW = (
    Path(__file__).resolve().parents[1] / ".github" / "workflows" / "packages.yml"
)


def recovery_run_is_eligible(run: dict, jobs: list[dict]) -> bool:
    """Model the fail-closed run/job contract used by the recovery workflow."""

    if run.get("status") != "completed":
        return False
    if run.get("conclusion") not in {"success", "failure"}:
        return False

    build_jobs = [job for job in jobs if job.get("name") == "Build packages"]
    return (
        len(build_jobs) == 1
        and build_jobs[0].get("status") == "completed"
        and build_jobs[0].get("conclusion") == "success"
    )


class PackagesWorkflowSourceGuardTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.workflow = WORKFLOW.read_text(encoding="utf-8")

    def test_nuget_publication_uses_oidc(self) -> None:
        self.assertIn("permissions:\n      actions: read\n      contents: read\n      id-token: write", self.workflow)
        self.assertIn("uses: NuGet/login@v1", self.workflow)
        self.assertIn("user: ${{ secrets.NUGET_USER }}", self.workflow)
        self.assertIn("steps.nuget_login.outputs.NUGET_API_KEY", self.workflow)
        self.assertNotIn("secrets.NUGET_API_KEY", self.workflow)

    def test_recovery_skips_build_and_reuses_original_artifact(self) -> None:
        self.assertIn("recovery_run_id:", self.workflow)
        self.assertIn("recovery_version:", self.workflow)
        self.assertIn(
            "github.event_name != 'workflow_dispatch' || inputs.recovery_run_id == ''",
            self.workflow,
        )
        self.assertIn("run-id: ${{ inputs.recovery_run_id }}", self.workflow)
        self.assertIn("repository: ${{ github.repository }}", self.workflow)
        self.assertIn("name: elsa-template-packages", self.workflow)

    def test_recovery_validates_run_and_package_provenance_before_login(self) -> None:
        validation_start = self.workflow.index(
            "name: Validate original release run and package provenance"
        )
        login_start = self.workflow.index("name: NuGet login (OIDC)")
        validation = self.workflow[validation_start:login_start]

        for required_check in (
            '.repository.full_name // empty',
            '.event // empty',
            '.status // empty',
            '.conclusion // empty',
            '.head_sha // empty',
            'select(.name == "Build packages")',
            'test "$(jq \'length\' <<<"${build_jobs}")" = "1"',
            ".[0].status // empty",
            ".[0].conclusion // empty",
            'tag_ref="refs/tags/${RECOVERY_VERSION}"',
            'git rev-parse --verify "${tag_ref}^{commit}"',
            'Elsa.Templates.${RECOVERY_VERSION}.nupkg',
            'repository commit does not match the immutable release tag',
        ):
            self.assertIn(required_check, validation)

        self.assertLess(validation_start, login_start)

    def test_recovery_accepts_a_failed_run_when_build_succeeded(self) -> None:
        run = {
            "status": "completed",
            "conclusion": "failure",
        }
        jobs = [
            {"name": "Build packages", "status": "completed", "conclusion": "success"},
            {"name": "Publish to nuget.org", "status": "completed", "conclusion": "failure"},
        ]

        self.assertTrue(recovery_run_is_eligible(run, jobs))
        self.assertFalse(recovery_run_is_eligible({**run, "status": "in_progress"}, jobs))
        self.assertFalse(recovery_run_is_eligible({**run, "conclusion": "cancelled"}, jobs))
        self.assertFalse(
            recovery_run_is_eligible(
                run,
                jobs + [{"name": "Build packages", "status": "completed", "conclusion": "success"}],
            )
        )

    def test_normal_release_still_publishes_from_successful_build(self) -> None:
        self.assertIn("github.event_name == 'release' && needs.build.result == 'success'", self.workflow)
        self.assertIn("if: ${{ github.event_name == 'release' }}", self.workflow)


if __name__ == "__main__":
    unittest.main()
