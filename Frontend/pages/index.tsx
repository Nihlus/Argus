import { Layout } from '@/components/common/Layout'
import { InteractionViewLayout } from '@/components/common/InteractionViewLayout'
import { Card } from '@/components/common/Card'
import { Headline } from '@/components/common/Headline'
import { CardTitle } from '@/components/common/CardTitle'

export default function Home() {
  return (
    <Layout>
      <Headline>Argus</Headline>
      <InteractionViewLayout>
        <Card>
          <CardTitle>Your images</CardTitle>
        </Card>
        <Card>
          <CardTitle>Potential copyright violations</CardTitle>
        </Card>
      </InteractionViewLayout>
    </Layout>
  )
}
