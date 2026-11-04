import { readFile, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'
import { wave } from './media-fixtures'

async function selectSet(page: Page, name: string) {
  await page.getByRole('button', { name: new RegExp('^' + name + '( Active)?$') }).click()
}

test('editors manage shared categories, question media, external edits, and active sets', async ({ page, browser }, testInfo) => {
  await page.goto('/sets')
  for (const name of ['Christmas Party', 'Family Birthday']) {
    await page.getByLabel('New set name').fill(name)
    await page.getByRole('button', { name: 'Create set', exact: true }).click()
    await expect(page.getByLabel('Set name', { exact: true })).toHaveValue(name)
  }

  await page.getByRole('link', { name: 'Categories', exact: true }).click()
  await page.getByLabel('New category name').fill('Atlas')
  await page.getByRole('button', { name: 'Create category', exact: true }).click()
  await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Atlas')
  const categoryUrl = page.url()
  const categoryId = categoryUrl.split('/').at(-1)!
  await page.getByLabel('Christmas Party', { exact: true }).check()
  await page.getByLabel('Family Birthday', { exact: true }).check()

  await page.getByRole('button', { name: 'Add question', exact: true }).click()
  await page.getByLabel('Prompt', { exact: true }).fill('What is the capital of Denmark?')
  await page.getByLabel('Correct answer', { exact: true }).fill('Copenhagen')
  await page.getByLabel('Alternative answers (one per line)').fill('København\nCopenhague')
  await page.getByRole('button', { name: 'Save category', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Save category', exact: true })).toBeDisabled()

  const png = await page.evaluate(() => {
    const canvas = document.createElement('canvas')
    canvas.width = 16
    canvas.height = 16
    const context = canvas.getContext('2d')!
    context.fillStyle = '#149caa'
    context.fillRect(0, 0, 16, 16)
    return canvas.toDataURL().split(',')[1]
  })
  await page.getByRole('button', { name: 'Add question', exact: true }).click()
  await page.getByRole('combobox', { name: 'Question type' }).selectOption('image')
  await page.getByLabel('Prompt', { exact: true }).fill('Name this color.')
  await page.getByLabel('Correct answer', { exact: true }).fill('Teal')
  await page.getByLabel('Question media', { exact: true }).setInputFiles({
    name: 'teal.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64'),
  })
  await expect(page.getByText(/media\/.*\.png/)).toBeVisible()
  await page.getByRole('button', { name: 'Preview question', exact: true }).click()
  await expect.poll(() => page.getByRole('img', { name: 'Quiz question image' }).evaluate((element: HTMLImageElement) => element.naturalWidth)).toBe(16)
  await page.getByRole('button', { name: 'Save category', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Save category', exact: true })).toBeDisabled()

  await page.getByRole('button', { name: 'Add question', exact: true }).click()
  await page.getByRole('combobox', { name: 'Question type' }).selectOption('audio')
  await page.getByLabel('Prompt', { exact: true }).fill('Identify this audio clip.')
  await page.getByLabel('Correct answer', { exact: true }).fill('Silence')
  await page.getByLabel('Question media', { exact: true }).setInputFiles({
    name: 'silence.wav', mimeType: 'audio/wav', buffer: wave(),
  })
  await expect(page.getByText(/media\/.*\.wav/)).toBeVisible()
  await page.getByRole('button', { name: 'Save category', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Save category', exact: true })).toBeDisabled()

  const dataDirectory = (await (await page.request.get('/api/library')).json()).dataDirectory
  const file = join(dataDirectory, 'library', 'categories', categoryId, 'category.yaml')
  const yaml = await readFile(file, 'utf8')
  expect(yaml).toContain('Copenhague')
  await page.getByLabel('Description', { exact: true }).fill('Uncommitted browser edit')
  await writeFile(file, yaml.replace('name: Atlas', 'name: Atlas revised'))
  await expect(page.getByText('This category changed on disk.')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Save category', exact: true })).toBeDisabled()
  page.once('dialog', dialog => dialog.accept())
  await page.getByRole('button', { name: 'Load latest version', exact: true }).click()
  await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Atlas revised')
  const corrected = await readFile(file, 'utf8')
  await writeFile(file, 'name: [broken')
  await expect(page.getByRole('alert').filter({ hasText: 'Some library files need attention.' })).toBeVisible()
  await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Atlas revised')
  await writeFile(file, corrected)
  await page.getByRole('button', { name: 'Reload files', exact: true }).click()
  await expect(page.getByRole('alert').filter({ hasText: 'Some library files need attention.' })).toHaveCount(0)

  await page.getByRole('link', { name: 'Category sets', exact: true }).click()
  await selectSet(page, 'Christmas Party')
  await expect(page.getByRole('link', { name: 'Atlas revised', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Make active', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Active set', exact: true })).toBeDisabled()
  await selectSet(page, 'Family Birthday')
  await expect(page.getByRole('link', { name: 'Atlas revised', exact: true })).toBeVisible()
  await page.screenshot({ path: testInfo.outputPath('set-editor.png'), animations: 'disabled' })

  await page.getByRole('link', { name: 'Host', exact: true }).click()
  await expect(page.getByText('Active set Christmas Party')).toBeVisible()
  await page.getByLabel('Player A', { exact: true }).fill('Alex')
  await page.getByLabel('Player B', { exact: true }).fill('Sam')
  await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
  await page.getByRole('combobox', { name: 'Question order' }).selectOption('ordered')
  await page.getByRole('button', { name: 'Prepare duel' }).click()
  const audience = await browser.newContext()
  try {
    const display = await audience.newPage()
    await display.goto('/display')
    await expect(display.getByText('What is the capital of Denmark?')).toBeVisible()
    await page.getByRole('button', { name: 'Start', exact: true }).click()
    const sets = await page.context().newPage()
    try {
      await sets.goto('/sets')
      await selectSet(sets, 'Family Birthday')
      await sets.getByRole('button', { name: 'Make active', exact: true }).click()
      await expect(sets.getByRole('button', { name: 'Active set', exact: true })).toBeDisabled()
    } finally {
      await sets.close()
    }
    await expect(display.getByText('What is the capital of Denmark?')).toBeVisible()
    await page.getByRole('button', { name: /Correct/ }).click()
    await expect(display.getByText('Name this color.')).toBeVisible()
    await expect.poll(() => display.getByRole('img', { name: 'Quiz question image' }).evaluate((element: HTMLImageElement) => element.naturalWidth)).toBe(16)
    await page.getByRole('button', { name: /Correct/ }).click({ delay: 250 })
    await expect(display.getByText('Identify this audio clip.')).toBeVisible()
    await display.getByRole('button', { name: 'Enable automatic question audio' }).click()
    await expect.poll(() => display.locator('audio').evaluate((element: HTMLAudioElement) => element.currentTime)).toBeGreaterThan(0)
  } finally {
    await audience.close()
    await page.getByText('Duel options', { exact: true }).click()
    page.once('dialog', dialog => dialog.accept())
    await page.getByRole('button', { name: 'End duel', exact: true }).click()
    await page.getByRole('button', { name: 'Return to setup', exact: true }).click()
  }

  await expect(page.getByText('Active set Family Birthday')).toBeVisible()
  await page.getByRole('combobox', { name: 'Category', exact: true }).selectOption(categoryId)
  await page.goto(categoryUrl)
  await page.getByRole('button', { name: 'Duplicate independently' }).click()
  await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Atlas revised (copy)')
  await page.getByLabel('Category name', { exact: true }).fill('Independent Atlas')
  await page.getByRole('button', { name: 'Save category', exact: true }).click()
  await expect(page.getByRole('link', { name: 'Independent Atlas', exact: true })).toBeVisible()
  await page.goto(categoryUrl)
  await expect(page.getByLabel('Category name', { exact: true })).toHaveValue('Atlas revised')
  await page.getByLabel('Christmas Party', { exact: true }).uncheck()
  await page.getByRole('link', { name: 'Category sets', exact: true }).click()
  await selectSet(page, 'Family Birthday')
  await expect(page.getByRole('link', { name: 'Atlas revised', exact: true })).toBeVisible()
  await selectSet(page, 'Christmas Party')
  await expect(page.getByRole('link', { name: 'Atlas revised', exact: true })).toHaveCount(0)
})
