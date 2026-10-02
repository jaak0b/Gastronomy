export function combineReleases(...releases: (() => void)[]): () => void {
  return () => {
    for (const release of releases) {
      release()
    }
  }
}
