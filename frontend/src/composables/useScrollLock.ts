let locks = 0

export function lockScroll(): void {
  if (locks++ > 0) return
  const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth
  document.body.style.overflow = 'hidden'
  if (scrollbarWidth > 0) document.body.style.paddingRight = `${scrollbarWidth}px`
}

export function unlockScroll(): void {
  if (locks === 0 || --locks > 0) return
  document.body.style.overflow = ''
  document.body.style.paddingRight = ''
}
